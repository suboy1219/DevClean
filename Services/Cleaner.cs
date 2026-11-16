using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using DevClean.Models;
using DevClean.Utilities;

namespace DevClean.Services;

public sealed record CleanOutcome(
    bool Success,
    long FreedBytes,
    int DeletedFiles,
    int FailedFiles,
    string Detail);

public static class Cleaner
{
    private const uint SherbNoConfirmation = 0x00000001;
    private const uint SherbNoProgressUi = 0x00000002;
    private const uint SherbNoSound = 0x00000004;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private static readonly string[] ProtectedDirectories = BuildProtectedDirectories();

    public static Task<CleanOutcome> CleanAsync(CleanItem item, CancellationToken cancellationToken = default)
        => Task.Run(() => CleanCore(item, cancellationToken), cancellationToken);

    private static CleanOutcome CleanCore(CleanItem item, CancellationToken cancellationToken)
    {
        AppLog.Write($"CLEAN start id={item.Id} mode={item.Mode} risk={item.Risk}");

        try
        {
            return item.Mode switch
            {
                CleanMode.RecycleBin => EmptyRecycleBin(item),
                CleanMode.Command => RunCommand(item, cancellationToken),
                CleanMode.ReportOnly => new CleanOutcome(false, 0, 0, 0, "仅统计，不执行清理"),
                _ => DeletePaths(item, cancellationToken)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Write($"CLEAN error id={item.Id}: {ex}");
            return new CleanOutcome(false, 0, 0, 0, "清理失败：" + ex.Message);
        }
    }

    private static CleanOutcome DeletePaths(CleanItem item, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> paths = PathResolver.ExpandAll(item.Paths);
        long freed = 0;
        int deleted = 0;
        int failed = 0;
        int refused = 0;

        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                failed++;
                continue;
            }

            if (!IsAllowedTarget(fullPath, out string reason))
            {
                refused++;
                AppLog.Write($"CLEAN refused path={fullPath} reason={reason}");
                continue;
            }

            if (File.Exists(fullPath))
            {
                TryDeleteFile(fullPath, ref freed, ref deleted, ref failed);
            }
            else if (Directory.Exists(fullPath))
            {
                bool deleteRoot = item.Mode == CleanMode.DeleteTree;
                DeleteDirectory(fullPath, deleteRoot, ref freed, ref deleted, ref failed, cancellationToken);
            }
        }

        string detail = $"删除 {deleted} 个文件";
        if (failed > 0)
        {
            detail += $"，跳过 {failed} 个占用/无权限的文件";
        }

        if (refused > 0)
        {
            detail += $"，{refused} 个路径出于安全考虑被拒绝";
        }

        AppLog.Write($"CLEAN done id={item.Id} freed={freed} deleted={deleted} failed={failed} refused={refused}");
        return new CleanOutcome(deleted > 0 || failed == 0, freed, deleted, failed, detail);
    }

    public static (long Freed, int Deleted, int Failed) DeleteDirectoryContentsForTest(string path, CancellationToken cancellationToken = default)
    {
        long freed = 0;
        int deleted = 0;
        int failed = 0;
        DeleteDirectory(path, false, ref freed, ref deleted, ref failed, cancellationToken);
        return (freed, deleted, failed);
    }

    private static void DeleteDirectory(
        string dir,
        bool deleteRoot,
        ref long freed,
        ref int deleted,
        ref int failed,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        FileSystemInfo[] entries;
        try
        {
            entries = new DirectoryInfo(dir).GetFileSystemInfos();
        }
        catch
        {
            failed++;
            TryDeleteEmptyDirectory(dir);
            return;
        }

        foreach (FileSystemInfo entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry is FileInfo file)
            {
                TryDeleteFile(file.FullName, ref freed, ref deleted, ref failed);
                continue;
            }

            if (entry is not DirectoryInfo sub)
            {
                continue;
            }

            bool isLink;
            try
            {
                isLink = (sub.Attributes & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                failed++;
                continue;
            }

            if (isLink)
            {
                // Delete the link itself, never traverse into it.
                try
                {
                    Directory.Delete(sub.FullName, false);
                }
                catch
                {
                    failed++;
                }

                continue;
            }

            DeleteDirectory(sub.FullName, true, ref freed, ref deleted, ref failed, cancellationToken);
        }

        if (deleteRoot)
        {
            TryDeleteEmptyDirectory(dir);
        }
    }

    private static void TryDeleteFile(string path, ref long freed, ref int deleted, ref int failed)
    {
        long length = 0;
        try
        {
            var info = new FileInfo(path);
            length = info.Length;
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
            {
                info.Attributes &= ~FileAttributes.ReadOnly;
            }

            info.Delete();
            freed += length;
            deleted++;
        }
        catch
        {
            failed++;
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            Directory.Delete(path, false);
        }
        catch
        {
            // Still in use or not empty: leave it.
        }
    }

    private static bool IsAllowedTarget(string fullPath, out string reason)
    {
        reason = string.Empty;

        string normalized = fullPath.TrimEnd('\\');
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root) || normalized.Length <= root.TrimEnd('\\').Length)
        {
            reason = "目标不能是磁盘根目录";
            return false;
        }

        foreach (string blocked in ProtectedDirectories)
        {
            if (string.Equals(normalized, blocked, StringComparison.OrdinalIgnoreCase))
            {
                reason = "目标属于受保护目录";
                return false;
            }
        }

        string appDir = AppContext.BaseDirectory.TrimEnd('\\');
        if (appDir.StartsWith(normalized + "\\", StringComparison.OrdinalIgnoreCase))
        {
            reason = "目标包含程序自身目录";
            return false;
        }

        return true;
    }

    private static string[] BuildProtectedDirectories()
    {
        var list = new List<string>();

        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                list.Add(path.TrimEnd('\\'));
            }
        }

        string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        Add(systemDrive);
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.System));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

        return list
            .Where(p => p.Length > 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static CleanOutcome EmptyRecycleBin(CleanItem item)
    {
        long before = GetSystemDriveFreeSpace();

        int hr;
        try
        {
            hr = SHEmptyRecycleBin(IntPtr.Zero, null, SherbNoConfirmation | SherbNoProgressUi | SherbNoSound);
        }
        catch (Exception ex)
        {
            AppLog.Write("RECYCLE BIN error: " + ex);
            return new CleanOutcome(false, 0, 0, 0, "清空回收站失败：" + ex.Message);
        }

        long freed = Math.Max(0, GetSystemDriveFreeSpace() - before);
        bool success = hr == 0 || hr == 1 || hr == unchecked((int)0x8000FFFF);
        AppLog.Write($"RECYCLE BIN hr=0x{hr:X8} freed={freed}");

        return new CleanOutcome(
            success,
            freed,
            0,
            0,
            success ? "回收站已清空" : $"清空回收站返回代码 0x{hr:X8}");
    }

    private static CleanOutcome RunCommand(CleanItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CommandFile))
        {
            return new CleanOutcome(false, 0, 0, 0, "未配置命令");
        }

        if (item.RequiresCommand is not null && !PathResolver.CommandExists(item.RequiresCommand))
        {
            return new CleanOutcome(false, 0, 0, 0, "本机未安装对应工具，已跳过");
        }

        long before = GetSystemDriveFreeSpace();

        var startInfo = new ProcessStartInfo(item.CommandFile)
        {
            Arguments = item.CommandArguments ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            AppLog.Write($"COMMAND start failed {item.Id}: {ex.Message}");
            return new CleanOutcome(false, 0, 0, 0, "无法启动命令：" + ex.Message);
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        bool exited;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(TimeSpan.FromMinutes(20));
            try
            {
                process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
                exited = true;
            }
            catch (OperationCanceledException)
            {
                exited = false;
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // Already exited.
                }
            }
        }

        string output;
        try
        {
            output = (stdout.GetAwaiter().GetResult() + " " + stderr.GetAwaiter().GetResult()).Trim();
        }
        catch
        {
            output = string.Empty;
        }

        long freed = Math.Max(0, GetSystemDriveFreeSpace() - before);
        string compact = output.Replace("\r", " ").Replace("\n", " | ").Trim();
        if (compact.Length > 220)
        {
            compact = compact[..220] + "…";
        }

        bool success = exited && process.ExitCode == 0;
        if (!exited)
        {
            compact = "命令执行超时，已终止。" + compact;
        }
        else if (!success)
        {
            compact = $"命令退出码 {process.ExitCode}。" + compact;
        }

        AppLog.Write($"COMMAND done id={item.Id} exit={(exited ? process.ExitCode : -1)} freed={freed} output={compact}");
        return new CleanOutcome(success, freed, 0, 0, string.IsNullOrWhiteSpace(compact) ? "命令已执行" : compact);
    }

    private static long GetSystemDriveFreeSpace()
    {
        try
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }
}

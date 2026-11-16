using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DevClean.Models;
using DevClean.Utilities;

namespace DevClean.Services;

public static class Headless
{
    public static int Run(string[] args)
    {
        TextWriter writer = ConsoleBridge.CreateWriter();

        try
        {
            if (Has(args, "--help") || Has(args, "-h"))
            {
                PrintHelp(writer);
                return 0;
            }

            if (Has(args, "--selftest"))
            {
                return RunSelfTest(writer);
            }

            if (Has(args, "--clean-safe"))
            {
                return RunCleanSafe(args, writer);
            }

            return RunScan(args, writer);
        }
        catch (Exception ex)
        {
            AppLog.Write("HEADLESS fatal: " + ex);
            writer.WriteLine("执行失败：" + ex.Message);
            writer.WriteLine("日志：" + AppLog.LogDirectory);
            return 1;
        }
        finally
        {
            (writer as IDisposable)?.Dispose();
        }
    }

    private static int RunScan(string[] args, TextWriter writer)
    {
        bool json = Has(args, "--json");
        string? outPath = GetOption(args, "--out");
        List<CleanItem> items = CleanupCatalog.Create();

        writer.WriteLine($"正在扫描 {items.Count} 个清理项…");
        SizeScanner.ScanAsync(items).GetAwaiter().GetResult();

        long total = items.Where(i => i.Available && !i.IsReportOnly).Sum(i => i.SizeBytes);
        int available = items.Count(i => i.Available);
        DriveInfo drive = GetSystemDrive();
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var output = new StringBuilder();
        if (json)
        {
            var payload = new
            {
                scannedAt = timestamp,
                drive = new
                {
                    name = drive.Name,
                    freeBytes = drive.AvailableFreeSpace,
                    totalBytes = drive.TotalSize
                },
                reclaimableBytes = total,
                items = items.Select(i => new
                {
                    i.Id,
                    i.Name,
                    i.Group,
                    i.Description,
                    mode = i.Mode.ToString(),
                    risk = i.Risk.ToString(),
                    i.RequiresAdmin,
                    i.Available,
                    i.SizeBytes,
                    size = ByteFormat.Size(i.SizeBytes),
                    i.Paths
                })
            };

            output.AppendLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        else
        {
            output.AppendLine($"DevClean 扫描报告  {timestamp}");
            output.AppendLine($"系统盘 {drive.Name}  可用 {ByteFormat.Size(drive.AvailableFreeSpace)} / 共 {ByteFormat.Size(drive.TotalSize)}");
            output.AppendLine($"可清理总计 {ByteFormat.Size(total)}  检测到 {available}/{items.Count} 项");
            output.AppendLine(new string('-', 72));

            foreach (var group in items.GroupBy(i => i.Group))
            {
                output.AppendLine();
                output.AppendLine($"[{group.Key}]");
                foreach (CleanItem item in group.Where(i => i.Available).OrderByDescending(i => i.SizeBytes))
                {
                    string size = item.IsCommand ? "命令   " : ByteFormat.Size(item.SizeBytes).PadLeft(8);
                    string risk = item.Risk switch
                    {
                        RiskLevel.Safe => "安全",
                        RiskLevel.Caution => "谨慎",
                        _ => "危险"
                    };
                    string admin = item.RequiresAdmin ? " [需管理员]" : string.Empty;
                    output.AppendLine($"  {size}  {risk}  {item.Name}{admin}");
                }
            }
        }

        string text = output.ToString();
        writer.Write(text);

        if (!string.IsNullOrWhiteSpace(outPath))
        {
            string full = Path.GetFullPath(outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
            File.WriteAllText(full, text, new UTF8Encoding(false));
            writer.WriteLine();
            writer.WriteLine("报告已写入：" + full);
        }

        return 0;
    }

    private static int RunCleanSafe(string[] args, TextWriter writer)
    {
        if (!Has(args, "--yes"))
        {
            writer.WriteLine("安全清理会真实删除文件。确认后请追加 --yes 参数重新执行：");
            writer.WriteLine("  DevClean.exe --clean-safe --yes");
            return 2;
        }

        List<CleanItem> items = CleanupCatalog.Create();
        writer.WriteLine($"正在扫描 {items.Count} 个清理项…");
        SizeScanner.ScanAsync(items).GetAwaiter().GetResult();

        var targets = items
            .Where(i => i.Available
                        && i.Risk == RiskLevel.Safe
                        && i.Mode != CleanMode.ReportOnly
                        && (!i.RequiresAdmin || Elevation.IsElevated))
            .OrderByDescending(i => i.SizeBytes)
            .ToList();

        if (targets.Count == 0)
        {
            writer.WriteLine("没有可自动清理的安全项。");
            return 0;
        }

        writer.WriteLine($"将清理 {targets.Count} 个安全项，预计 {ByteFormat.Size(targets.Sum(i => i.SizeBytes))}。");
        long freedTotal = 0;

        foreach (CleanItem item in targets)
        {
            writer.WriteLine($"  → {item.Name} ({ByteFormat.Size(item.SizeBytes)})");
            CleanOutcome outcome = Cleaner.CleanAsync(item).GetAwaiter().GetResult();
            freedTotal += outcome.FreedBytes;
            writer.WriteLine($"    {(outcome.Success ? "完成" : "部分失败")}：{outcome.Detail}，释放 {ByteFormat.Size(outcome.FreedBytes)}");
        }

        writer.WriteLine(new string('-', 72));
        writer.WriteLine($"清理完成，共计释放 {ByteFormat.Size(freedTotal)}。");
        return 0;
    }

    private static int RunSelfTest(TextWriter writer)
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "DevCleanSelftest-" + Guid.NewGuid().ToString("N"));
        bool pass = true;

        try
        {
            Directory.CreateDirectory(Path.Combine(sandbox, "nested", "deeper"));
            File.WriteAllBytes(Path.Combine(sandbox, "a.bin"), new byte[1024]);
            File.WriteAllBytes(Path.Combine(sandbox, "nested", "b.bin"), new byte[2048]);
            File.WriteAllBytes(Path.Combine(sandbox, "nested", "deeper", "c.bin"), new byte[4096]);
            long expected = 1024 + 2048 + 4096;
            long measured = PathResolver.GetSize(sandbox);

            writer.WriteLine($"size measure: {measured} / {expected}");
            pass &= measured == expected;

            var (freed, deleted, failed) = Cleaner.DeleteDirectoryContentsForTest(sandbox);
            writer.WriteLine($"delete: freed={freed} deleted={deleted} failed={failed}");
            pass &= freed == expected && deleted == 3 && failed == 0;

            bool rootKept = Directory.Exists(sandbox);
            bool empty = rootKept && !Directory.EnumerateFileSystemEntries(sandbox).Any();
            writer.WriteLine($"root kept: {rootKept}, empty: {empty}");
            pass &= rootKept && empty;
        }
        catch (Exception ex)
        {
            writer.WriteLine("selftest exception: " + ex);
            pass = false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(sandbox))
                {
                    Directory.Delete(sandbox, true);
                }
            }
            catch
            {
                // Best effort.
            }
        }

        writer.WriteLine(pass ? "SELFTEST: PASS" : "SELFTEST: FAIL");
        return pass ? 0 : 1;
    }

    private static DriveInfo GetSystemDrive()
    {
        string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        return new DriveInfo(root);
    }

    private static bool Has(string[] args, string name)
        => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? GetOption(string[] args, string name)
    {
        int index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }

        return null;
    }

    private static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("""
DevClean - 面向开发者的 Windows 磁盘清理工具

用法:
  DevClean.exe                         打开图形界面
  DevClean.exe --demo                  用示例数据打开界面（不会读取本机文件）
  DevClean.exe --scan                  扫描并输出可清理项
  DevClean.exe --scan --json           以 JSON 输出扫描结果
  DevClean.exe --scan --out report.txt 扫描结果写入文件
  DevClean.exe --clean-safe --yes      清理所有"安全"级别的项目（无需确认）
  DevClean.exe --selftest              运行删除逻辑自检
  DevClean.exe --help                  显示帮助

安全说明:
  * 只清理内置白名单中的缓存目录，不触碰文档、代码与个人文件
  * 正在被占用的文件会自动跳过
  * 高风险项目默认不勾选，且需要额外确认
""");
    }
}

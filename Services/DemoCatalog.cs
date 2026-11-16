using DevClean.Models;

namespace DevClean.Services;

public static class DemoCatalog
{
    private const long Gb = 1024L * 1024 * 1024;

    public static List<CleanItem> Create()
    {
        var items = new List<CleanItem>();

        void Add(
            int groupOrder,
            string group,
            string id,
            string name,
            string description,
            RiskLevel risk,
            bool requiresAdmin,
            long sizeBytes,
            CleanMode mode = CleanMode.DeleteContents,
            string? note = null)
        {
            items.Add(new CleanItem
            {
                GroupOrder = groupOrder,
                Group = group,
                Id = id,
                Name = name,
                Description = description,
                Risk = risk,
                RequiresAdmin = requiresAdmin,
                Mode = mode,
                Paths = new[] { @"C:\Users\demo\AppData\Local\Example" },
                Note = note ?? "演示数据，仅用于界面展示，不会访问本机文件。",
                SizeBytes = sizeBytes,
                Available = true
            });
        }

        Add(0, "Windows 系统", "demo-user-temp", "用户临时文件",
            "当前账户的 %TEMP% 目录，安装包与程序解压残留集中在这里",
            RiskLevel.Safe, false, (long)(2.36 * Gb));

        Add(0, "Windows 系统", "demo-windows-update", "Windows 更新下载缓存",
            "已下载但已安装的更新包，通常有数 GB",
            RiskLevel.Caution, true, (long)(6.82 * Gb));

        Add(0, "Windows 系统", "demo-recycle-bin", "回收站",
            "清空所有驱动器的回收站",
            RiskLevel.Caution, false, (long)(1.24 * Gb), CleanMode.RecycleBin);

        Add(0, "Windows 系统", "demo-thumbnails", "缩略图缓存",
            "资源管理器生成的文件预览缩略图数据库",
            RiskLevel.Safe, false, (long)(0.812 * Gb), CleanMode.DeleteFiles);

        Add(0, "Windows 系统", "demo-crash-dumps", "应用崩溃转储",
            "%LOCALAPPDATA%\\CrashDumps 下各程序生成的 dump 文件",
            RiskLevel.Safe, false, (long)(0.436 * Gb));

        Add(0, "Windows 系统", "demo-windows-old", "旧版 Windows 备份（Windows.old）",
            "系统升级前的完整旧系统备份，通常 10 GB 以上",
            RiskLevel.Danger, true, (long)(18.6 * Gb), CleanMode.DeleteTree,
            "删除后无法回退到升级前的 Windows 版本，且不可恢复。");

        Add(0, "Windows 系统", "demo-hibernation", "休眠文件（hiberfil.sys）",
            "关闭休眠与快速启动，释放与内存容量相当的磁盘空间",
            RiskLevel.Danger, true, 0, CleanMode.Command,
            "会同时关闭快速启动。需要恢复时，在管理员命令行执行 powercfg /hibernate on。");

        Add(1, "开发工具缓存", "demo-npm", "npm 缓存",
            "npm 包下载缓存与解压残留",
            RiskLevel.Safe, false, (long)(4.71 * Gb));

        Add(1, "开发工具缓存", "demo-nuget", "NuGet 全局包目录",
            "~/.nuget/packages 中所有已还原的 NuGet 包",
            RiskLevel.Caution, false, (long)(3.18 * Gb));

        Add(1, "开发工具缓存", "demo-gradle", "Gradle 缓存",
            "Gradle 依赖、构建缓存与 wrapper 下载",
            RiskLevel.Caution, false, (long)(2.64 * Gb));

        Add(1, "开发工具缓存", "demo-jetbrains", "JetBrains IDE 缓存与索引",
            "IntelliJ IDEA / PyCharm / WebStorm 等的缓存、索引与日志",
            RiskLevel.Caution, false, (long)(3.87 * Gb));

        Add(1, "开发工具缓存", "demo-vscode", "VS Code 缓存",
            "VS Code 的 Cache、CachedData、GPU 缓存与日志",
            RiskLevel.Safe, false, (long)(0.942 * Gb));

        Add(1, "开发工具缓存", "demo-docker", "Docker 未使用数据",
            "执行 docker system prune，清理停止的容器、未使用镜像与构建缓存",
            RiskLevel.Caution, false, 0, CleanMode.Command,
            "不会删除命名卷和正在运行的容器；被删除的镜像需要重新拉取或构建。");

        Add(2, "浏览器与常用应用", "demo-chrome", "Chrome 浏览器缓存",
            "所有 Chrome 配置文件的网页缓存、代码缓存与 GPU 缓存",
            RiskLevel.Safe, false, (long)(1.46 * Gb));

        Add(2, "浏览器与常用应用", "demo-edge", "Edge 浏览器缓存",
            "所有 Edge 配置文件的网页缓存与代码缓存",
            RiskLevel.Safe, false, (long)(1.12 * Gb));

        Add(2, "浏览器与常用应用", "demo-firefox", "Firefox 浏览器缓存",
            "Firefox 配置文件的 cache2 目录",
            RiskLevel.Safe, false, (long)(0.684 * Gb));

        Add(3, "其他与排查", "demo-iis-logs", "IIS 访问日志",
            "C:\\Windows\\System32\\LogFiles 下的 IIS 站点日志",
            RiskLevel.Caution, true, (long)(0.218 * Gb));

        Add(3, "其他与排查", "demo-downloads", "下载文件夹（仅统计）",
            "Downloads 目录通常混有安装包、镜像与大文件，本工具不会自动删除",
            RiskLevel.Caution, false, (long)(6.4 * Gb), CleanMode.ReportOnly,
            "请手动确认后再删除，避免误删需要的文件。");

        return items;
    }
}

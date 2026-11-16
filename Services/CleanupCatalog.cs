using DevClean.Models;

namespace DevClean.Services;

public static class CleanupCatalog
{
    private const int GroupSystem = 0;
    private const int GroupDev = 1;
    private const int GroupApps = 2;
    private const int GroupOther = 3;

    public static List<CleanItem> Create()
    {
        var items = new List<CleanItem>();

        void Add(
            int groupOrder,
            string group,
            string id,
            string name,
            string description,
            CleanMode mode,
            RiskLevel risk,
            bool requiresAdmin,
            string[] paths,
            string? note = null,
            string? commandFile = null,
            string? commandArguments = null,
            string? requiresCommand = null)
        {
            if (requiresCommand is not null && !PathResolver.CommandExists(requiresCommand))
            {
                return;
            }

            items.Add(new CleanItem
            {
                GroupOrder = groupOrder,
                Group = group,
                Id = id,
                Name = name,
                Description = description,
                Mode = mode,
                Risk = risk,
                RequiresAdmin = requiresAdmin,
                Paths = paths,
                Note = note ?? string.Empty,
                CommandFile = commandFile,
                CommandArguments = commandArguments,
                RequiresCommand = requiresCommand
            });
        }

        // ---------------- Windows 系统 ----------------
        Add(GroupSystem, "Windows 系统", "user-temp", "用户临时文件",
            "当前账户的 %TEMP% 目录，安装包与程序解压残留集中在这里",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%TEMP%" },
            "正在被程序占用的文件会自动跳过，可放心重复清理。");

        Add(GroupSystem, "Windows 系统", "windows-temp", "系统临时文件",
            "C:\\Windows\\Temp 下的系统级临时文件",
            CleanMode.DeleteContents, RiskLevel.Safe, true,
            new[] { @"%SystemRoot%\Temp" },
            "需要管理员权限；被系统占用的文件会跳过。");

        Add(GroupSystem, "Windows 系统", "windows-update", "Windows 更新下载缓存",
            "已下载但已安装的更新包，通常有数 GB",
            CleanMode.DeleteContents, RiskLevel.Caution, true,
            new[] { @"%SystemRoot%\SoftwareDistribution\Download" },
            "需要在管理员权限下运行。正在安装更新时部分文件会跳过，不影响系统。");

        Add(GroupSystem, "Windows 系统", "delivery-optimization", "传递优化缓存",
            "Windows 更新 P2P 分发缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, true,
            new[] { @"%SystemRoot%\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache" });

        Add(GroupSystem, "Windows 系统", "wer", "Windows 错误报告",
            "程序崩溃后生成的错误报告与转储队列",
            CleanMode.DeleteContents, RiskLevel.Safe, true,
            new[]
            {
                @"%LOCALAPPDATA%\Microsoft\Windows\WER",
                @"%ProgramData%\Microsoft\Windows\WER"
            });

        Add(GroupSystem, "Windows 系统", "crash-dumps", "应用崩溃转储",
            "%LOCALAPPDATA%\\CrashDumps 下各程序生成的 dump 文件",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\CrashDumps" },
            "排查崩溃时需要保留 dump，平时可以清理。");

        Add(GroupSystem, "Windows 系统", "minidump", "系统小型转储",
            "蓝屏后生成的 Minidump 文件",
            CleanMode.DeleteContents, RiskLevel.Safe, true,
            new[] { @"%SystemRoot%\Minidump" });

        Add(GroupSystem, "Windows 系统", "memory-dump", "系统内存转储文件",
            "C:\\Windows\\MEMORY.DMP，单个文件可能非常大",
            CleanMode.DeleteFiles, RiskLevel.Caution, true,
            new[] { @"%SystemRoot%\MEMORY.DMP" },
            "仅在排查蓝屏问题时需要，删除后无法恢复。");

        Add(GroupSystem, "Windows 系统", "prefetch", "预读取缓存",
            "Windows 启动与程序预读数据",
            CleanMode.DeleteContents, RiskLevel.Caution, true,
            new[] { @"%SystemRoot%\Prefetch" },
            "系统会自动重建，清理后前几天启动可能略慢。");

        Add(GroupSystem, "Windows 系统", "windows-logs", "Windows 日志与 CBS 记录",
            "C:\\Windows\\Logs 下的安装、更新与服务日志",
            CleanMode.DeleteContents, RiskLevel.Safe, true,
            new[] { @"%SystemRoot%\Logs" },
            "正在写入的日志会跳过；不影响系统运行。");

        Add(GroupSystem, "Windows 系统", "panther", "Windows 安装日志",
            "系统安装/升级过程留下的日志与临时文件",
            CleanMode.DeleteContents, RiskLevel.Caution, true,
            new[] { @"%SystemRoot%\Panther" },
            "升级失败需要回溯时可能用到这些日志。");

        Add(GroupSystem, "Windows 系统", "windows-old", "旧版 Windows 备份（Windows.old）",
            "系统升级前的完整旧系统备份，通常 10 GB 以上",
            CleanMode.DeleteTree, RiskLevel.Danger, true,
            new[] { @"%SystemDrive%\Windows.old" },
            "删除后无法回退到升级前的 Windows 版本，且不可恢复。");

        Add(GroupSystem, "Windows 系统", "windows-upgrade-temp", "Windows 升级残留",
            "$Windows.~BT / $Windows.~WS 升级临时目录",
            CleanMode.DeleteTree, RiskLevel.Caution, true,
            new[]
            {
                @"%SystemDrive%\$Windows.~BT",
                @"%SystemDrive%\$Windows.~WS"
            },
            "删除后无法通过系统回滚撤销最近一次功能更新。");

        Add(GroupSystem, "Windows 系统", "recycle-bin", "回收站",
            "清空所有驱动器的回收站",
            CleanMode.RecycleBin, RiskLevel.Caution, false,
            Array.Empty<string>(),
            "回收站中的文件将被永久删除，无法恢复。");

        Add(GroupSystem, "Windows 系统", "thumbnails", "缩略图缓存",
            "资源管理器生成的文件预览缩略图数据库",
            CleanMode.DeleteFiles, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db" },
            "被资源管理器占用时会跳过，重新打开文件夹会自动重建。");

        Add(GroupSystem, "Windows 系统", "dism-cleanup", "WinSxS 组件清理",
            "用 DISM 清理已被取代的 Windows 组件版本",
            CleanMode.Command, RiskLevel.Caution, true,
            Array.Empty<string>(),
            "耗时约 5-20 分钟，期间请不要关机；清理后无法卸载已安装的更新。",
            "dism.exe", "/Online /Cleanup-Image /StartComponentCleanup");

        Add(GroupSystem, "Windows 系统", "hibernation", "休眠文件（hiberfil.sys）",
            "关闭休眠与快速启动，释放与内存容量相当的磁盘空间",
            CleanMode.Command, RiskLevel.Danger, true,
            Array.Empty<string>(),
            "会同时关闭快速启动。需要恢复时，在管理员命令行执行 powercfg /hibernate on。",
            "powercfg.exe", "/hibernate off");

        Add(GroupSystem, "Windows 系统", "pagefile", "虚拟内存文件（仅统计）",
            "pagefile.sys 由系统管理，本工具不会删除",
            CleanMode.ReportOnly, RiskLevel.Caution, false,
            new[] { @"%SystemDrive%\pagefile.sys" },
            "如需调整，请在「系统属性 → 高级 → 性能 → 虚拟内存」中设置。");

        // ---------------- 开发工具缓存 ----------------
        Add(GroupDev, "开发工具缓存", "npm-cache", "npm 缓存",
            "npm 包下载缓存与解压残留",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\npm-cache", @"%APPDATA%\npm-cache" },
            "清理后 npm install 会重新下载，不影响已安装的 node_modules。");

        Add(GroupDev, "开发工具缓存", "yarn-cache", "Yarn 缓存",
            "Yarn Classic 与 Berry 的包缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\Yarn\Cache", @"%LOCALAPPDATA%\Yarn\Berry\cache" });

        Add(GroupDev, "开发工具缓存", "pnpm-store", "pnpm 存储",
            "pnpm 全局内容寻址存储与元数据缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\pnpm\store", @"%LOCALAPPDATA%\pnpm-cache" },
            "项目 node_modules 通过硬链接引用存储，删除存储不会删除项目文件，但下次安装会重新下载。");

        Add(GroupDev, "开发工具缓存", "pip-cache", "pip 缓存",
            "Python 包 wheel 下载缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\pip\Cache" });

        Add(GroupDev, "开发工具缓存", "nuget-http", "NuGet HTTP 缓存",
            "NuGet 包下载与插件缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[]
            {
                @"%LOCALAPPDATA%\NuGet\v3-cache",
                @"%LOCALAPPDATA%\NuGet\Cache",
                @"%LOCALAPPDATA%\NuGet\plugins-cache"
            },
            "只清理下载缓存，不影响 ~/.nuget/packages 里已还原的包。");

        Add(GroupDev, "开发工具缓存", "nuget-packages", "NuGet 全局包目录",
            "~/.nuget/packages 中所有已还原的 NuGet 包",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%USERPROFILE%\.nuget\packages" },
            ".NET 项目常用包，删除后首次 restore 需要重新下载，离线环境请勿清理。");

        Add(GroupDev, "开发工具缓存", "gradle-cache", "Gradle 缓存",
            "Gradle 依赖、构建缓存与 wrapper 下载",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%USERPROFILE%\.gradle\caches" },
            "Android / Java 项目首次构建会明显变慢。");

        Add(GroupDev, "开发工具缓存", "maven-repo", "Maven 本地仓库",
            "~/.m2/repository 中已下载的依赖",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%USERPROFILE%\.m2\repository" },
            "删除后 Maven 构建需要重新下载依赖。");

        Add(GroupDev, "开发工具缓存", "cargo-cache", "Cargo 注册表缓存",
            "Rust crate 源码与下载缓存",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%USERPROFILE%\.cargo\registry\cache", @"%USERPROFILE%\.cargo\registry\src" },
            "删除后 cargo build 会重新下载 crate。");

        Add(GroupDev, "开发工具缓存", "rustup-downloads", "rustup 安装包缓存",
            "rustup 下载的组件安装包",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%USERPROFILE%\.rustup\downloads" });

        Add(GroupDev, "开发工具缓存", "go-build", "Go 构建缓存",
            "go build / go test 的编译缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\go-build" },
            "清理后首次编译会变慢。");

        Add(GroupDev, "开发工具缓存", "go-mod", "Go 模块缓存",
            "%USERPROFILE%\\go\\pkg\\mod 中下载的依赖模块",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%USERPROFILE%\go\pkg\mod" },
            "删除后构建需要重新下载模块。");

        Add(GroupDev, "开发工具缓存", "pub-cache", "Dart / Flutter 包缓存",
            "Pub 全局包缓存",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%LOCALAPPDATA%\Pub\Cache" },
            "Flutter 项目首次 pub get 会重新下载。");

        Add(GroupDev, "开发工具缓存", "jetbrains-cache", "JetBrains IDE 缓存与索引",
            "IntelliJ IDEA / PyCharm / WebStorm 等的缓存、索引与日志",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[]
            {
                @"%LOCALAPPDATA%\JetBrains\*\caches",
                @"%LOCALAPPDATA%\JetBrains\*\index",
                @"%LOCALAPPDATA%\JetBrains\*\log"
            },
            "建议关闭 IDE 后清理；重新打开项目会重建索引，首次加载较慢。");

        Add(GroupDev, "开发工具缓存", "vscode-cache", "VS Code 缓存",
            "VS Code 的 Cache、CachedData、GPU 缓存与日志",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[]
            {
                @"%APPDATA%\Code\Cache",
                @"%APPDATA%\Code\CachedData",
                @"%APPDATA%\Code\Code Cache",
                @"%APPDATA%\Code\GPUCache",
                @"%APPDATA%\Code\logs"
            },
            "不会影响插件、设置与已打开的工作区。");

        Add(GroupDev, "开发工具缓存", "vs-cache", "Visual Studio 缓存",
            "VS 组件模型缓存、扩展缓存与反馈日志",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[]
            {
                @"%LOCALAPPDATA%\Microsoft\VisualStudio\*\ComponentModelCache",
                @"%LOCALAPPDATA%\Temp\VSFeedbackVSRTCLogs"
            },
            "下次启动 VS 会自动重建缓存。");

        Add(GroupDev, "开发工具缓存", "aspnet-temp", "ASP.NET 临时编译文件",
            ".NET Framework 运行时动态编译的临时程序集",
            CleanMode.DeleteContents, RiskLevel.Safe, true,
            new[]
            {
                @"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files",
                @"%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\Temporary ASP.NET Files"
            },
            "IIS 站点下次访问时会自动重新编译。");

        Add(GroupDev, "开发工具缓存", "electron-cache", "Electron 打包缓存",
            "electron / electron-builder 下载的运行时包",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\electron\Cache", @"%LOCALAPPDATA%\electron-builder\Cache" },
            "下次打包会重新下载 Electron 运行时。");

        Add(GroupDev, "开发工具缓存", "docker-prune", "Docker 未使用数据",
            "执行 docker system prune，清理停止的容器、未使用镜像与构建缓存",
            CleanMode.Command, RiskLevel.Caution, false,
            Array.Empty<string>(),
            "不会删除命名卷和正在运行的容器；被删除的镜像需要重新拉取或构建。",
            "docker.exe", "system prune -f", "docker.exe");

        Add(GroupDev, "开发工具缓存", "unity-cache", "Unity 缓存与资源包",
            "Unity 缓存与 Asset Store 已下载资源",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%LOCALAPPDATA%\Unity\cache", @"%APPDATA%\Unity\Asset Store-5.x" },
            "Asset Store 资源需要重新下载。");

        Add(GroupDev, "开发工具缓存", "unreal-ddc", "Unreal 派生数据缓存",
            "Unreal Engine DerivedDataCache",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%LOCALAPPDATA%\UnrealEngine\Common\DerivedDataCache" },
            "清理后首次打开项目需要重新生成着色器与资源派生数据。");

        Add(GroupDev, "开发工具缓存", "android-snapshots", "Android 模拟器快照",
            "AVD 模拟器保存的运行快照",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%USERPROFILE%\.android\avd\*\snapshots" },
            "删除后模拟器冷启动，首次开机变慢。");

        Add(GroupDev, "开发工具缓存", "ccache", "ccache 编译缓存",
            "C/C++ 编译结果缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\ccache" });

        // ---------------- 浏览器与常用应用 ----------------
        Add(GroupApps, "浏览器与常用应用", "chrome-cache", "Chrome 浏览器缓存",
            "所有 Chrome 配置文件的网页缓存、代码缓存与 GPU 缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[]
            {
                @"%LOCALAPPDATA%\Google\Chrome\User Data\*\Cache",
                @"%LOCALAPPDATA%\Google\Chrome\User Data\*\Code Cache",
                @"%LOCALAPPDATA%\Google\Chrome\User Data\*\GPUCache",
                @"%LOCALAPPDATA%\Google\Chrome\User Data\*\Service Worker\CacheStorage"
            },
            "不会退出登录、不删除书签与历史记录；建议先关闭 Chrome。");

        Add(GroupApps, "浏览器与常用应用", "edge-cache", "Edge 浏览器缓存",
            "所有 Edge 配置文件的网页缓存与代码缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[]
            {
                @"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Cache",
                @"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Code Cache",
                @"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\GPUCache"
            },
            "建议先关闭 Edge。");

        Add(GroupApps, "浏览器与常用应用", "brave-cache", "Brave 浏览器缓存",
            "Brave 网页与代码缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[]
            {
                @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data\*\Cache",
                @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data\*\Code Cache"
            });

        Add(GroupApps, "浏览器与常用应用", "firefox-cache", "Firefox 浏览器缓存",
            "Firefox 配置文件的 cache2 目录",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\Mozilla\Firefox\Profiles\*\cache2" },
            "建议先关闭 Firefox。");

        Add(GroupApps, "浏览器与常用应用", "inet-cache", "WebView2 / IE 缓存",
            "Windows 内置 WebView2 与 IE 模式网页缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\Microsoft\Windows\INetCache" },
            "部分文件被应用占用时会跳过。");

        Add(GroupApps, "浏览器与常用应用", "office-cache", "Office 文档缓存",
            "Office 打开文档时生成的本地缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\Microsoft\Office\16.0\OfficeFileCache" });

        Add(GroupApps, "浏览器与常用应用", "store-cache", "Microsoft Store 缓存",
            "商店应用安装与页面缓存",
            CleanMode.DeleteContents, RiskLevel.Caution, false,
            new[] { @"%LOCALAPPDATA%\Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\LocalCache" },
            "若商店出现异常，清理缓存通常可以恢复。");

        Add(GroupApps, "浏览器与常用应用", "nvidia-cache", "NVIDIA 着色器缓存",
            "显卡驱动生成 DirectX / OpenGL 着色器缓存",
            CleanMode.DeleteContents, RiskLevel.Safe, false,
            new[] { @"%LOCALAPPDATA%\NVIDIA\DXCache", @"%LOCALAPPDATA%\NVIDIA\GLCache" },
            "游戏首次运行会重新编译着色器。");

        // ---------------- 其他与排查 ----------------
        Add(GroupOther, "其他与排查", "iis-logs", "IIS 访问日志",
            "C:\\Windows\\System32\\LogFiles 下的 IIS 站点日志",
            CleanMode.DeleteContents, RiskLevel.Caution, true,
            new[] { @"%SystemRoot%\System32\LogFiles" },
            "正在写入的日志会跳过；如用于审计请先备份。");

        Add(GroupOther, "其他与排查", "downloads", "下载文件夹（仅统计）",
            "Downloads 目录通常混有安装包、镜像与大文件，本工具不会自动删除",
            CleanMode.ReportOnly, RiskLevel.Caution, false,
            new[] { @"%USERPROFILE%\Downloads" },
            "请手动确认后再删除，避免误删需要的文件。");

        return items;
    }
}

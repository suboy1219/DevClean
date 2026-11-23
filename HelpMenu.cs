using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DevClean;

internal static class HelpMenu
{
    private static bool _checking;

    public static void Show(FrameworkElement target, Window owner)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        var instructions = new MenuItem { Header = "使用说明" };
        instructions.Click += (_, _) => Open(UpdateService.ProjectUrl + "#readme", owner);
        menu.Items.Add(instructions);
        var check = new MenuItem { Header = _checking ? "正在检测更新…" : "检测更新", IsEnabled = !_checking };
        check.Click += async (_, _) =>
        {
            if (_checking) return;
            _checking = true;
            target.IsEnabled = false;
            var tooltip = target.ToolTip;
            target.ToolTip = "正在检测更新…";
            try
            {
                var result = await new UpdateService().CheckAsync();
                if (!owner.IsLoaded) return;
                if (result?.IsUpdateAvailable == true)
                {
                    if (MessageBox.Show(owner,
                        $"发现新版本 {result.TagName}（当前 {result.CurrentVersion.ToString(3)}）。\n\n是否前往 GitHub 发布页下载？",
                        "检测更新", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        Open(result.ReleaseUrl, owner);
                }
                else
                {
                    MessageBox.Show(owner, result == null ? "暂时没有可用的正式发布版。" :
                        $"当前已是最新版本（{result.CurrentVersion.ToString(3)}）。",
                        "检测更新", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException
                or OperationCanceledException or System.Text.Json.JsonException)
            {
                if (owner.IsLoaded)
                    MessageBox.Show(owner, "无法检测更新，请检查网络连接后重试。GitHub 暂时不可用或请求次数受限时也可能出现此提示。",
                        "检测更新", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _checking = false;
                target.IsEnabled = true;
                target.ToolTip = tooltip;
            }
        };
        menu.Items.Add(check);
        var about = new MenuItem { Header = "关于" };
        about.Click += (_, _) => MessageBox.Show(owner,
            $"DevClean {UpdateService.CurrentVersion.ToString(3)}\n\n{UpdateService.ProjectUrl}",
            "关于 DevClean", MessageBoxButton.OK, MessageBoxImage.Information);
        menu.Items.Add(about);
        menu.IsOpen = true;
    }

    private static void Open(string url, Window owner)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(owner, $"无法打开浏览器，请手动访问：\n{url}", "DevClean",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

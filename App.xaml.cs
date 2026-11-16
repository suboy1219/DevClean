using System.Windows;
using System.Windows.Threading;
using DevClean.Services;
using DevClean.Utilities;

namespace DevClean;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        if (e.Args.Any(a => string.Equals(a, "--demo", StringComparison.OrdinalIgnoreCase)))
        {
            var demoWindow = new MainWindow(demo: true);
            MainWindow = demoWindow;
            demoWindow.Show();
            return;
        }

        if (e.Args.Length > 0 && e.Args[0].StartsWith("--", StringComparison.Ordinal))
        {
            int exitCode = Headless.Run(e.Args);
            Shutdown(exitCode);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Write("UNHANDLED: " + e.Exception);
        MessageBox.Show(
            "程序遇到未处理的错误：\n\n" + e.Exception.Message + "\n\n日志位置：\n" + AppLog.LogDirectory,
            "DevClean",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}

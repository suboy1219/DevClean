using System.Diagnostics;
using System.Security.Principal;

namespace DevClean.Services;

public static class Elevation
{
    public static bool IsElevated { get; } = DetectElevated();

    private static bool DetectElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool RelaunchElevated(string? arguments = null)
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = AppContext.BaseDirectory
            };
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

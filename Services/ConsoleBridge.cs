using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DevClean.Services;

internal static class ConsoleBridge
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    public static TextWriter CreateWriter()
    {
        try
        {
            AttachConsole(-1);
        }
        catch
        {
            // No parent console; keep going with file output.
        }

        try
        {
            Stream stream = Console.OpenStandardOutput();
            return new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch
        {
            return TextWriter.Null;
        }
    }
}

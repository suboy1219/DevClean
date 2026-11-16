namespace DevClean.Utilities;

public static class ByteFormat
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Size(long bytes)
    {
        if (bytes <= 0)
        {
            return "—";
        }

        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {Units[unit]}"
            : value >= 100
                ? $"{value:0} {Units[unit]}"
                : value >= 10
                    ? $"{value:0.0} {Units[unit]}"
                    : $"{value:0.00} {Units[unit]}";
    }
}

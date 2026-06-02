using System.Globalization;

static class Format
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public static string Count(long value) => value.ToString("N0", Culture);

    public static string Currency(double value) => value.ToString("C2", Culture);

    public static string Bytes(long bytes)
    {
        ReadOnlySpan<string> units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit += 1;
        }

        return $"{value:N2} {units[unit]}";
    }

    public static string Elapsed(TimeSpan elapsed) => elapsed.ToString(@"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture);
}

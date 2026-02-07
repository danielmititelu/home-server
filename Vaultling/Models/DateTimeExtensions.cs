namespace Vaultling.Models;

public static class DateTimeExtensions
{
    public static string ToIsoDateString(this DateTime date)
    {
        return date.ToString("yyyy-MM-dd");
    }

    public static string ToIsoDateString(this DateTimeOffset date)
    {
        return date.ToString("yyyy-MM-dd");
    }
}

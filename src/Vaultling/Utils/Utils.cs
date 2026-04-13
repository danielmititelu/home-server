namespace Vaultling.Utils;

using System.Globalization;

public static class Utils
{
    public static IEnumerable<T> ParseCsv<T>(
        IEnumerable<string> csvLines,
        Func<string[], T> mapper,
        int maxColumnSplit = -1)
    {
        return csvLines
            .Skip(1) // Skip header
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = maxColumnSplit > 0
                    ? line.Split(',', maxColumnSplit)
                    : line.Split(',');
                return mapper(parts);
            });
    }

    public static string ResolveYearPath(string pathTemplate, int year)
    {
        if (string.IsNullOrEmpty(pathTemplate))
            return "";

        return pathTemplate.Replace("{year}", year.ToString(CultureInfo.InvariantCulture));
    }

    private static readonly CultureInfo Romanian = new("ro-RO");

    public static string GetRelativeDateLabel(DateTime eventDate, DateTime today)
    {
        var dayOffset = (eventDate.Date - today.Date).Days;

        return dayOffset switch
        {
            0 => "Azi",
            1 => "Mâine",
            < 7 => CapitalizeFirst(eventDate.ToString("dddd", Romanian)),
            _ => $"{GetArticulatedDayName(eventDate)} următoare"
        };
    }

    public static string GetRelativeDateTimeLabel(DateTime eventDate, DateTime today)
    {
        var dateLabel = GetRelativeDateLabel(eventDate, today);
        var timeLabel = eventDate.TimeOfDay == TimeSpan.Zero
            ? ""
            : $" la {eventDate:HH:mm}";

        return $"{dateLabel}{timeLabel}";
    }

    public static string GetCalendarReportMonthAnchor(int year, int month)
    {
        var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
        return $"{month:00} - {monthName}";
    }

    public static string GetCalendarReportMonthLink(DateTime date)
    {
        var anchor = GetCalendarReportMonthAnchor(date.Year, date.Month);
        return $"[[{date.Year}-calendar-report#{anchor}| toate evenimentele]]";
    }

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static string GetArticulatedDayName(DateTime date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "Lunea",
        DayOfWeek.Tuesday => "Marțea",
        DayOfWeek.Wednesday => "Miercurea",
        DayOfWeek.Thursday => "Joia",
        DayOfWeek.Friday => "Vinerea",
        DayOfWeek.Saturday => "Sâmbăta",
        DayOfWeek.Sunday => "Duminica",
        _ => CapitalizeFirst(date.ToString("dddd", Romanian))
    };

    public static string BuildMonthlyCalendar(
        int year,
        int month,
        Func<int, string> dayCellResolver)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var firstDay = new DateTime(year, month, 1);

        var currentWeek = new List<string>();
        var startDayOfWeek = (int)firstDay.DayOfWeek;
        startDayOfWeek = startDayOfWeek == 0 ? 6 : startDayOfWeek - 1;

        var weekRows = new List<string>();

        for (int i = 0; i < startDayOfWeek; i++)
            currentWeek.Add("     ");

        for (int day = 1; day <= daysInMonth; day++)
        {
            currentWeek.Add(dayCellResolver(day));

            if (currentWeek.Count == 7)
            {
                weekRows.Add($"| {string.Join(" | ", currentWeek)} |");
                currentWeek.Clear();
            }
        }

        if (currentWeek.Count > 0)
        {
            while (currentWeek.Count < 7)
                currentWeek.Add("     ");

            weekRows.Add($"| {string.Join(" | ", currentWeek)} |");
        }

        return $"""
            | Mon | Tue | Wed | Thu | Fri | Sat | Sun |
            |-----|-----|-----|-----|-----|-----|-----|
            {string.Join("\n", weekRows)}
            """;
    }
}

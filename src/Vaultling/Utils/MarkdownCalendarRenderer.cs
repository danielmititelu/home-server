namespace Vaultling.Utils;

public static class MarkdownCalendarRenderer
{
    public static void AppendCalendarGrid(
        List<string> sections,
        int year,
        int month,
        Func<int, string> dayCellResolver)
    {
        sections.Add("| Mon | Tue | Wed | Thu | Fri | Sat | Sun |");
        sections.Add("|-----|-----|-----|-----|-----|-----|-----|");

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var firstDay = new DateTime(year, month, 1);

        var currentWeek = new List<string>();
        var startDayOfWeek = (int)firstDay.DayOfWeek;
        startDayOfWeek = startDayOfWeek == 0 ? 6 : startDayOfWeek - 1;

        for (int i = 0; i < startDayOfWeek; i++)
            currentWeek.Add("     ");

        for (int day = 1; day <= daysInMonth; day++)
        {
            currentWeek.Add(dayCellResolver(day));

            if (currentWeek.Count == 7)
            {
                sections.Add($"| {string.Join(" | ", currentWeek)} |");
                currentWeek.Clear();
            }
        }

        if (currentWeek.Count > 0)
        {
            while (currentWeek.Count < 7)
                currentWeek.Add("     ");

            sections.Add($"| {string.Join(" | ", currentWeek)} |");
        }
    }
}
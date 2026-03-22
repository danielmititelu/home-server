namespace Vaultling.Utils;

public static class MarkdownCalendarRenderer
{
    private const int CellWidth = 5;

    public static void AppendCalendarGrid(
        List<string> sections,
        int year,
        int month,
        Func<int, string> dayCellResolver)
    {
        var headerCells = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }
            .Select(c => c.PadRight(CellWidth));
        sections.Add($"| {string.Join(" | ", headerCells)} |");
        sections.Add("|-------|-------|-------|-------|-------|-------|-------|");

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var firstDay = new DateTime(year, month, 1);

        var currentWeek = new List<string>();
        var startDayOfWeek = (int)firstDay.DayOfWeek;
        startDayOfWeek = startDayOfWeek == 0 ? 6 : startDayOfWeek - 1;

        for (int i = 0; i < startDayOfWeek; i++)
            currentWeek.Add(new string(' ', CellWidth));

        for (int day = 1; day <= daysInMonth; day++)
        {
            currentWeek.Add(dayCellResolver(day).PadRight(CellWidth));

            if (currentWeek.Count == 7)
            {
                sections.Add($"| {string.Join(" | ", currentWeek)} |");
                currentWeek.Clear();
            }
        }

        if (currentWeek.Count > 0)
        {
            while (currentWeek.Count < 7)
                currentWeek.Add(new string(' ', CellWidth));

            sections.Add($"| {string.Join(" | ", currentWeek)} |");
        }
    }
}
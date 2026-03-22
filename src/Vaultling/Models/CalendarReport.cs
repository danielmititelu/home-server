namespace Vaultling.Models;

using System.Globalization;

public record MonthlyCalendarSummary(
    int Month,
    int Year,
    List<CalendarOccurrence> Events
);

public record CalendarReport(List<MonthlyCalendarSummary> Months)
{
    public IEnumerable<string> ToMarkdownLines()
    {
        var sections = new List<string>();

        foreach (var month in Months)
        {
            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Month);
            sections.Add($"## {month.Month:00} - {monthName}");
            sections.Add("");

            sections.Add($"**Total Events: {month.Events.Count}**");
            sections.Add("");

            foreach (var evt in month.Events.OrderBy(e => e.Date))
            {
                var timeStr = evt.Date.TimeOfDay == TimeSpan.Zero
                    ? ""
                    : $" at {evt.Date:HH:mm}";
                sections.Add($"- {evt.Date.Day:00}{timeStr}: {evt.Note}");
            }

            sections.Add("");
        }

        return sections;
    }
}

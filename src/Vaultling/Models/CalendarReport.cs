namespace Vaultling.Models;

using System.Globalization;
using Vaultling.Utils;

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
        var today = DateTime.Today;

        foreach (var month in Months)
        {
            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Month);
            var isCurrentMonth = month.Year == today.Year && month.Month == today.Month;
            sections.Add($"## {month.Month:00} - {monthName}{(isCurrentMonth ? " ⭐" : "")}");
            sections.Add("");

            var eventDays = month.Events.Select(e => e.Date.Day).ToHashSet();

            MarkdownCalendarRenderer.AppendCalendarGrid(
                sections,
                month.Year,
                month.Month,
                day =>
                {
                    var isToday = isCurrentMonth && day == today.Day;
                    return isToday
                        ? $"🔵 {day:00}"
                        : eventDays.Contains(day) ? $"✅ {day:00}" : $"⬜ {day:00}";
                });

            if (month.Events.Count > 0)
            {
                sections.Add("");
                foreach (var evt in month.Events.OrderBy(e => e.Date))
                {
                    var timeStr = evt.Date.TimeOfDay == TimeSpan.Zero
                        ? ""
                        : $" at {evt.Date:HH:mm}";
                    sections.Add($"- {evt.Date.Day:00}{timeStr}: {evt.Note}");
                }
            }

            sections.Add("");
        }

        return sections;
    }
}

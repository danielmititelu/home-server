namespace Vaultling.Services;

using System.Globalization;
using Vaultling.Utils;

public class CalendarService(CalendarRepository calendarRepository, TimeProvider timeProvider)
{
    public void ProduceCalendarReport()
    {
        var currentYear = timeProvider.GetLocalNow().Year;

        foreach (var year in Enumerable.Range(currentYear, 3))
        {
            var occurrences = calendarRepository.ReadCalendarOccurrences(year).ToList();

            var eventsByMonth = occurrences
                .GroupBy(o => o.Date.Month)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Date).ToList());

            var months = Enumerable.Range(1, 12)
                .Select(m => new MonthlyCalendarSummary(
                    Month: m,
                    Year: year,
                    Events: eventsByMonth.GetValueOrDefault(m, [])
                ))
                .ToList();

            var report = new CalendarReport(months);
            calendarRepository.WriteCalendarReport(year, GenerateMarkdownForCalendarReport(report));
        }
    }

    private static List<string> GenerateMarkdownForCalendarReport(CalendarReport report)
    {
        var sections = new List<string>();
        var today = DateTime.Today;

        foreach (var month in report.Months)
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

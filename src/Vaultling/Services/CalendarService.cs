namespace Vaultling.Services;
using Vaultling.Utils;

public class CalendarService(CalendarRepository calendarRepository, TimeProvider timeProvider)
{
    public void ProduceCalendarReport()
    {
        var currentYear = timeProvider.GetLocalNow().Year;

        foreach (var year in Enumerable.Range(currentYear, 3))
        {
            var occurrences = calendarRepository.CollectCalendarOccurrencesForYear(year).ToList();

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

            calendarRepository.WriteCalendarReport(year, GenerateMarkdownForCalendarReport(months));
        }
    }

    private static string GenerateMarkdownForCalendarReport(List<MonthlyCalendarSummary> months)
    {
        var sections = new List<string>();
        var today = DateTime.Today;

        foreach (var month in months)
        {
            var isCurrentMonth = month.Year == today.Year && month.Month == today.Month;
            var monthAnchor = Utils.GetCalendarReportMonthAnchor(month.Year, month.Month);
            var currentMonthSymbol = isCurrentMonth ? " 🔵" : "";

            var eventDays = month.Events.Select(e => e.Date.Day).ToHashSet();
            var calendarGrid = Utils.BuildMonthlyCalendar(
                month.Year,
                month.Month,
                day =>
                {
                    var isToday = isCurrentMonth && day == today.Day;
                    var prefix = isToday ? "🔵" : eventDays.Contains(day) ? "📅" : "⬜";
                    return $"{prefix} {day:00}";
                });

            var eventsBlock = month.Events.Count > 0
                ? "\n" + string.Join("\n", month.Events.OrderBy(e => e.Date).Select(evt =>
                {
                    var timeStr = evt.Date.TimeOfDay == TimeSpan.Zero ? "" : $" at {evt.Date:HH:mm}";
                    var eventText = $"{evt.Date.Day:00}{timeStr}: {evt.Note}";
                    var renderedEventText = evt.Cancelled ? $"~~{eventText}~~" : eventText;
                    return $"- {renderedEventText}";
                }))
                : "";

            var monthSection = $"""
                ## {monthAnchor}{currentMonthSymbol}

                {calendarGrid}{eventsBlock}

                """;

            sections.Add(monthSection);
        }

        return string.Join("", sections);
    }
}

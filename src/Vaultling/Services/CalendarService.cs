namespace Vaultling.Services;

public class CalendarService(CalendarRepository calendarRepository, TimeProvider timeProvider)
{
    public void ProduceCalendarReport()
    {
        var currentYear = timeProvider.GetLocalNow().Year;
        var occurrences = calendarRepository.ReadCalendarOccurrences(currentYear).ToList();

        var eventsByMonth = occurrences
            .GroupBy(o => o.Date.Month)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Date).ToList());

        var months = Enumerable.Range(1, 12)
            .Select(m => new MonthlyCalendarSummary(
                Month: m,
                Year: currentYear,
                Events: eventsByMonth.GetValueOrDefault(m, [])
            ))
            .ToList();

        var report = new CalendarReport(months);
        calendarRepository.WriteCalendarReport(report);
    }
}

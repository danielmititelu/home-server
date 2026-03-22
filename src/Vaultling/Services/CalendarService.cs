namespace Vaultling.Services;

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
            calendarRepository.WriteCalendarReport(year, report);
        }
    }
}

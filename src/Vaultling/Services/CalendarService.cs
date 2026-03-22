namespace Vaultling.Services;

public class CalendarService(CalendarRepository calendarRepository, TimeProvider timeProvider)
{
    public void ProduceCalendarReport()
    {
        var currentYear = timeProvider.GetLocalNow().Year;
        var occurrences = calendarRepository.ReadCalendarOccurrences(currentYear).ToList();

        var months = occurrences
            .GroupBy(o => o.Date.Month)
            .OrderBy(g => g.Key)
            .Select(monthGroup => new MonthlyCalendarSummary(
                Month: monthGroup.Key,
                Year: currentYear,
                Events: monthGroup.OrderBy(e => e.Date).ToList()
            ))
            .ToList();

        var report = new CalendarReport(months);
        calendarRepository.WriteCalendarReport(report);
    }
}

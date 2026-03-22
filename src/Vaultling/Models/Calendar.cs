namespace Vaultling.Models;

public record MonthlyCalendarSummary(
    int Month,
    int Year,
    List<CalendarOccurrence> Events
);

public record CalendarReport(List<MonthlyCalendarSummary> Months);

public record CalendarEvent(string Date, string Note);

public record RecurringEvent(string Type, string Schedule, string Note);

public record CalendarOccurrence(DateTime Date, string Note);


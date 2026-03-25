namespace Vaultling.Models;

public record MonthlyCalendarSummary(
    int Month,
    int Year,
    List<CalendarOccurrence> Events
);

public record CalendarReport(List<MonthlyCalendarSummary> Months);

public record CalendarEvent(string Date, string Note, bool Cancelled = false);

public record RecurringEvent(string Type, string Schedule, string Note, bool Cancelled = false, DateTime? CycleStart = null, int? CycleCount = null, DateTime? CycleEnd = null);

public record CalendarOccurrence(DateTime Date, string Note, bool Cancelled = false);


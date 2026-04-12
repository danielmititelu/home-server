namespace Vaultling.Models;

public record MonthlyCalendarSummary(
    int Month,
    int Year,
    List<CalendarOccurrence> Events
);

public record CalendarReport(List<MonthlyCalendarSummary> Months);

public record RecurringEvent(string Type, string Schedule, string Note, bool Cancelled = false, int? CycleCount = null, string? CycleExpenseMatch = null);

public record CalendarOccurrence(DateTime Date, string Note, bool Cancelled = false);


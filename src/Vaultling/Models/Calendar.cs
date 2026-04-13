namespace Vaultling.Models;

public record MonthlyCalendarSummary(
    int Month,
    int Year,
    List<CalendarOccurrence> Events
);

public abstract record RecurringEvent(string Note);

public record WeeklyRecurringEvent(DayOfWeek Day, TimeOnly Time, string Note, int? CycleCount = null, string? CycleExpenseCategory = null, string? CycleExpenseDesc = null)
    : RecurringEvent(Note);

public record YearlyRecurringEvent(int Month, int Day, string Note)
    : RecurringEvent(Note);

public record CalendarOccurrence(DateTime Date, string Note, bool Cancelled = false);


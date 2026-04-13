namespace Vaultling.Models;

public record MonthlyCalendarSummary(
    int Month,
    int Year,
    List<CalendarOccurrence> Events
);

public abstract record RecurringEvent(string Note, bool Cancelled = false);

public record WeeklyRecurringEvent(DayOfWeek Day, TimeOnly Time, string Note, bool Cancelled = false, int? CycleCount = null, string? CycleExpenseCategory = null, string? CycleExpenseDesc = null)
    : RecurringEvent(Note, Cancelled);

public record YearlyRecurringEvent(int Month, int Day, string Note, bool Cancelled = false)
    : RecurringEvent(Note, Cancelled);

public record CalendarOccurrence(DateTime Date, string Note, bool Cancelled = false);


namespace Vaultling.Models;

public record DailyWorkout(string Exercise, string Reps);
public record DailyExpense(string Category, decimal Amount, string Description);
public enum DailySectionName
{
    Date,
    Weather,
    Workout,
    Todo,
    Expenses,
    Calendar
}

public record DailyEntry(
    DateTimeOffset Date,
    IEnumerable<DailyWorkout> Workouts,
    IEnumerable<string> Todos,
    IEnumerable<DailyExpense> Expenses,
    IEnumerable<CalendarOccurrence>? CalendarEvents = null,
    string City = "");

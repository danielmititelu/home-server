namespace Vaultling.Models;

public record ExpenseLog(int Month, int Day, string Category, decimal Amount, string Description);
public record ExpenseReport(List<MonthlyExpenseSummary> Months);
public record MonthlyExpenseSummary(
    int Month,
    List<CategoryExpenseTotal> Categories,
    decimal Total
);
public record CategoryExpenseTotal(string Category, decimal Amount);

public record WorkoutLog(string Month, string Day, string Type, string Reps);

public record DailyWorkout(string Exercise, string Reps);
public record DailyExpense(string Category, decimal Amount, string Description);
public record DailyFile(DateTime Date, IEnumerable<DailyWorkout> Workouts, IEnumerable<DailyExpense> Expenses);

public enum DailySectionName
{
    Date,
    Workout,
    Expenses
}

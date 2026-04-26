namespace Vaultling.Models;

public record ExpenseLog(int Year, int Month, int Day, string Category, decimal Amount, string Description);

public record CategoryExpenseTotal(string Category, decimal Amount);

public record MonthlyExpenseSummary(
    int Month,
    List<CategoryExpenseTotal> Categories,
    decimal Total
);

public record ExpenseReport(List<MonthlyExpenseSummary> Months);

namespace Vaultling.Services;

using Vaultling.Models;
using Vaultling.Services.Repositories;

public class ExpenseReportService(ExpenseRepository expenseRepository)
{
    public void Generate()
    {
        var expenses = expenseRepository.ReadExpenses();

        var months = expenses
            .GroupBy(e => e.Month)
            .OrderBy(g => g.Key)
            .Select(monthGroup => new MonthlyExpenseSummary(
                Month: monthGroup.Key,
                Categories: monthGroup
                    .GroupBy(e => e.Category)
                    .OrderBy(g => g.Key)
                    .Select(catGroup => new CategoryExpenseTotal(
                        Category: catGroup.Key,
                        Amount: catGroup.Sum(e => e.Amount)
                    ))
                    .ToList(),
                Total: monthGroup.Sum(e => e.Amount)
            ))
            .ToList();

        var report = new ExpenseReport(months);
        expenseRepository.WriteExpenseReport(report);
    }
}

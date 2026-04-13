namespace Vaultling.Services;

using System.Globalization;

public class ExpenseService(ExpenseRepository expenseRepository)
{
    public void ProduceExpenseReport()
    {
        var expenses = expenseRepository.ReadCurrentYearExpenses();

        var months = expenses
            .GroupBy(e => e.Month)
            .OrderBy(g => g.Key)
            .Select(monthGroup => new MonthlyExpenseSummary(
                Month: monthGroup.Key,
                Categories: [.. monthGroup
                    .GroupBy(e => e.Category)
                    .OrderBy(g => g.Key)
                    .Select(catGroup => new CategoryExpenseTotal(
                        Category: catGroup.Key,
                        Amount: catGroup.Sum(e => e.Amount)
                    ))],
                Total: monthGroup.Sum(e => e.Amount)
            ))
            .ToList();

        var report = new ExpenseReport(months);
        expenseRepository.WriteExpenseReport(GenerateExpenseMarkdownReport(report));
    }

    private static string GenerateExpenseMarkdownReport(ExpenseReport report)
    {
        var sections = new List<string>();

        foreach (var month in report.Months)
        {
            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Month);
            var categoryLines = string.Join("\n", month.Categories.Select(c => $"- {c.Category}: {c.Amount:0.00} RON"));

            var monthSection = $"""
                ## {month.Month:00} - {monthName}
                {categoryLines}
                - total: {month.Total:0.00} RON
                """;

            sections.Add(monthSection);
        }

        return string.Join("\n", sections);
    }
}

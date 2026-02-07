namespace Vaultling.Services;

using System.Globalization;

public class ExpenseReportService(VaultRepository vaultRepository)
{
    public void Generate()
    {
        var expenses = vaultRepository.ReadExpenses();

        var reportLines = expenses
            .GroupBy(e => e.Month)
            .OrderBy(g => g.Key)
            .SelectMany(monthGroup =>
            {
                var month = monthGroup.Key;
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
                var categoryLines = monthGroup
                    .GroupBy(e => e.Category)
                    .OrderBy(g => g.Key)
                    .Select(catGroup => $"- {catGroup.Key}: {catGroup.Sum(e => e.Amount):0.00} ron");
                var totalLine = $"- total: {monthGroup.Sum(e => e.Amount):0.00} ron";
                return new[] { $"## {month:00} - {monthName}" }
                    .Concat(categoryLines)
                    .Concat([totalLine]);
            })
            .ToList();

        vaultRepository.WriteExpensesReport(reportLines);
    }
}

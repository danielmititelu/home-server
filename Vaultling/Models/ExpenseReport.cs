namespace Vaultling.Models;

using System.Globalization;

public record ExpenseLog(int Month, int Day, string Category, decimal Amount, string Description)
{
    public string ToCsvLine()
    {
        return $"{Month},{Day},{Category},{Amount},{Description}";
    }

    public static IEnumerable<ExpenseLog> Parse(IEnumerable<string> csvLines)
    {
        return csvLines
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = line.Split(',');
                return new ExpenseLog(
                    Month: int.Parse(parts[0]),
                    Day: int.Parse(parts[1]),
                    Category: parts[2],
                    Amount: decimal.Parse(parts[3]),
                    Description: parts[4]
                );
            });
    }
}

public record CategoryExpenseTotal(string Category, decimal Amount);

public record MonthlyExpenseSummary(
    int Month,
    List<CategoryExpenseTotal> Categories,
    decimal Total
);

public record ExpenseReport(List<MonthlyExpenseSummary> Months)
{
    public IEnumerable<string> ToMarkdownLines()
    {
        var sections = new List<string>();
        
        foreach (var month in Months)
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
        
        return string.Join("\n", sections).Split('\n');
    }
}

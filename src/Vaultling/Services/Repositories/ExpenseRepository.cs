namespace Vaultling.Services.Repositories;

public class ExpenseRepository(IOptions<ExpenseOptions> options)
{
    private readonly ExpenseOptions _options = options.Value;

    public IEnumerable<ExpenseLog> ReadExpenses()
    {
        var lines = File.ReadLines(_options.DataFile);
        return ParseExpenseLogs(lines);
    }

    public void AppendExpenses(IEnumerable<ExpenseLog> expenses)
    {
        var lines = expenses.Select(ToCsvLine).ToList();
        if (lines.Count == 0)
        {
            return;
        }
        File.AppendAllLines(_options.DataFile, lines);
    }

    public void WriteExpenseReport(IEnumerable<string> markdownLines)
    {
        File.WriteAllLines(_options.ReportFile, markdownLines);
    }

    public static string ToCsvLine(ExpenseLog expense)
    {
        return $"{expense.Month},{expense.Day},{expense.Category},{expense.Amount},{expense.Description}";
    }

    public static IEnumerable<ExpenseLog> ParseExpenseLogs(IEnumerable<string> csvLines)
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
                    Category: parts[2].ToLower(),
                    Amount: decimal.Parse(parts[3]),
                    Description: parts[4]
                );
            });
    }
}

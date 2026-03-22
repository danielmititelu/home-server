namespace Vaultling.Services.Repositories;

using Vaultling.Utils;

public class ExpenseRepository(IOptions<ExpenseOptions> options)
{
    private readonly ExpenseOptions _options = options.Value;

    public IEnumerable<ExpenseLog> ReadExpenses()
    {
        return Utils.ParseCsv(File.ReadLines(_options.DataFile), parts => new ExpenseLog(
            Month: int.Parse(parts[0]),
            Day: int.Parse(parts[1]),
            Category: parts[2].ToLower(),
            Amount: decimal.Parse(parts[3]),
            Description: parts[4]
        ));
    }

    public void AppendExpenses(IEnumerable<ExpenseLog> expenses)
    {
        var lines = expenses
            .Select(expense => $"{expense.Month},{expense.Day},{expense.Category},{expense.Amount},{expense.Description}")
            .ToList();
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
}

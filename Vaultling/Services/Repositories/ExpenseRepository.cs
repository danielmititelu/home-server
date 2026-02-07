namespace Vaultling.Services.Repositories;

public class ExpenseRepository(IOptions<ExpenseOptions> options)
{
    private readonly ExpenseOptions _options = options.Value;

    public IEnumerable<ExpenseLog> ReadExpenses()
    {
        var lines = File.ReadLines(_options.DataFile);
        return ExpenseLog.Parse(lines);
    }

    public void AppendExpenses(IEnumerable<ExpenseLog> expenses)
    {
        var lines = expenses.Select(e => e.ToCsvLine()).ToList();
        if (lines.Count == 0)
        {
            return;
        }
        File.AppendAllLines(_options.DataFile, lines);
    }

    public void WriteExpenseReport(ExpenseReport report)
    {
        File.WriteAllLines(_options.ReportFile, report.ToMarkdownLines());
    }
}

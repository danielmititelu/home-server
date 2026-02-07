namespace Vaultling.Services.Repositories;

using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;

public class ExpenseRepository(IOptions<ExpenseOptions> options)
{
    private readonly ExpenseOptions _options = options.Value;

    public IEnumerable<ExpenseLog> ReadExpenses()
    {
        return File.ReadLines(_options.DataFile).ParseExpenseLogs();
    }

    public void AppendExpenses(IEnumerable<ExpenseLog> expenses)
    {
        var lines = expenses.Select(e => $"{e.Month},{e.Day},{e.Category},{e.Amount},{e.Description}").ToList();
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

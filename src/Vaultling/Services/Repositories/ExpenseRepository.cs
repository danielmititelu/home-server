namespace Vaultling.Services.Repositories;

using Vaultling.Utils;

public class ExpenseRepository(IOptions<ExpenseOptions> options)
{
    private readonly ExpenseOptions _options = options.Value;
    private List<ExpenseLog>? _cachedRecentExpenses;

    public IEnumerable<ExpenseLog> ReadCurrentYearExpenses()
        => ParseExpenseFile(_options.CurrentYearDataFile);

    public IEnumerable<ExpenseLog> ReadRecentExpenses()
    {
        if (_cachedRecentExpenses != null)
            return _cachedRecentExpenses;

        var previousYearExpenses = string.IsNullOrEmpty(_options.PreviousYearDataFile) ? [] : ParseExpenseFile(_options.PreviousYearDataFile);
        _cachedRecentExpenses = [.. previousYearExpenses, .. ReadCurrentYearExpenses()];
        return _cachedRecentExpenses;
    }

    public ExpenseLog? FindLatestExpense(string category, string descriptionContains)
        => ReadRecentExpenses()
            .Where(e =>
                e.Category.Contains(category, StringComparison.OrdinalIgnoreCase) &&
                e.Description.Contains(descriptionContains, StringComparison.OrdinalIgnoreCase))
            .MaxBy(e => (e.Month, e.Day));

    private static IEnumerable<ExpenseLog> ParseExpenseFile(string file)
    {
        if (!File.Exists(file)) return [];
        return Utils.ParseCsv(File.ReadLines(file), parts =>
        {
            if (parts.Length < 5
                || !int.TryParse(parts[0], out var month)
                || !int.TryParse(parts[1], out var day)
                || !decimal.TryParse(parts[3], System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                Console.Error.WriteLine($"[ExpenseRepository] Skipping malformed row: '{string.Join(",", parts)}'");
                return null;
            }
            return new ExpenseLog(Month: month, Day: day, Category: parts[2].ToLower(), Amount: amount, Description: parts[4]);
        }).OfType<ExpenseLog>();
    }

    public void AppendExpenses(IEnumerable<ExpenseLog> expenses)
    {
        var lines = expenses
            .Select(expense => $"{expense.Month},{expense.Day},{expense.Category.ToLower()},{expense.Amount},{expense.Description}")
            .ToList();
        if (lines.Count == 0)
        {
            return;
        }
        File.AppendAllLines(_options.CurrentYearDataFile, lines);
        _cachedRecentExpenses = null;
    }

    public void WriteExpenseReport(IEnumerable<string> markdownLines)
    {
        File.WriteAllLines(_options.ReportFile, markdownLines);
    }
}

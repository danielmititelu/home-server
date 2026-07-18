namespace Vaultling.Services.Repositories;

using System.Globalization;
using System.Text.RegularExpressions;
using Vaultling.Utils;

public partial class ExpenseRepository(IOptions<ExpenseOptions> options)
{
    [GeneratedRegex(@"\b(\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2})?)\b\s*->\s*\b(\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2})?)\b", RegexOptions.Compiled)]
    private static partial Regex RangeDateInDescriptionRegex();

    [GeneratedRegex(@"\s+\b(?:pe|at|on|in|la|spre|to)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "")]
    private static partial Regex ConnectorWordRegex();

    private readonly ExpenseOptions _options = options.Value;
    private List<ExpenseLog>? _cachedRecentExpenses;

    public IEnumerable<ExpenseLog> ReadCurrentYearExpenses()
        => ParseExpenseFile(_options.CurrentYearDataFile, _options.CurrentYear);

    public IEnumerable<ExpenseLog> ReadRecentExpenses()
    {
        if (_cachedRecentExpenses != null)
            return _cachedRecentExpenses;

        var previousYearExpenses = string.IsNullOrEmpty(_options.PreviousYearDataFile)
            ? []
            : ParseExpenseFile(_options.PreviousYearDataFile, _options.CurrentYear - 1);
        _cachedRecentExpenses = [.. previousYearExpenses, .. ReadCurrentYearExpenses()];
        return _cachedRecentExpenses;
    }

    public ExpenseLog? FindLatestExpense(string category, string descriptionContains)
        => ReadRecentExpenses()
            .Where(e =>
                e.Category.Contains(category, StringComparison.OrdinalIgnoreCase) &&
                e.Description.Contains(descriptionContains, StringComparison.OrdinalIgnoreCase))
            .MaxBy(e => (e.Year, e.Month, e.Day));

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

    public void WriteExpenseReport(string markdown)
    {
        File.WriteAllText(_options.CurrentYearReportFile, markdown);
    }

    public string? GetTravelCityForDate(DateTime date)
    {
        foreach (var expense in ReadRecentExpenses())
        {
            var description = expense.Description.Trim();

            var rangeMatch = RangeDateInDescriptionRegex().Match(description);
            if (!rangeMatch.Success) continue;

            if (!DateTime.TryParse(rangeMatch.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var depDate))
                continue;
            if (!DateTime.TryParse(rangeMatch.Groups[2].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var retDate))
                continue;

            if (date.Date < depDate.Date || date.Date >= retDate.Date) continue;

            var notePart = description[..rangeMatch.Index];
            notePart = ConnectorWordRegex().Replace(notePart, "").Trim();
            if (string.IsNullOrEmpty(notePart)) continue;

            var words = notePart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;

            return words[^1];
        }

        return null;
    }

    private static IEnumerable<ExpenseLog> ParseExpenseFile(string file, int year)
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
            return new ExpenseLog(Year: year, Month: month, Day: day, Category: parts[2].ToLower(), Amount: amount, Description: parts[4]);
        }).OfType<ExpenseLog>();
    }
}

using System.Globalization;

var expensesFilePath = "/srv/obsidian/MyVault/Finance/2026-expenses-csv.md";
var reportFilePath = "/srv/obsidian/MyVault/Finance/2026-expenses-report.md";

var expenses = File.ReadAllLines(expensesFilePath)
    .Skip(1)
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .Select(line =>
    {
        var parts = line.Split(',');
        return new Expense(
            Month: int.Parse(parts[0]),
            Day: int.Parse(parts[1]),
            Category: parts[2],
            Amount: decimal.Parse(parts[3]),
            Description: parts[4]
        );
    })
    .ToList();

var grouped = expenses
    .GroupBy(e => new { e.Month, e.Category })
    .OrderBy(g => g.Key.Month)
    .ThenBy(g => g.Key.Category);

var reportLines = new List<string>(expenses.Count);

int? currentMonth = null;

foreach (var group in grouped)
{
    if (currentMonth != group.Key.Month)
    {
        currentMonth = group.Key.Month;
        var monthName = CultureInfo.CurrentCulture
            .DateTimeFormat
            .GetMonthName(currentMonth.Value);
        reportLines.Add($"## {currentMonth:00} - {monthName}");
    }

    reportLines.Add($"- {group.Key.Category}: {group.Sum(e => e.Amount):0.00} ron");
}

File.WriteAllLines(reportFilePath, reportLines);

record Expense(int Month, int Day, string Category, decimal Amount, string Description);

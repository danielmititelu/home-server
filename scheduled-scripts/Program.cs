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
            .Concat(new[] { totalLine });
    })
    .ToList();

File.WriteAllLines(reportFilePath, reportLines);

record Expense(int Month, int Day, string Category, decimal Amount, string Description);

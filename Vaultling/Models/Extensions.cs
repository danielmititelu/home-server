namespace Vaultling.Models;

using System.Globalization;

public static class Extensions
{
    public static IEnumerable<WorkoutLog> ToWorkoutLogs(this DailyFile dailyFile)
    {
        return dailyFile.Workouts
            .Where(w => !string.IsNullOrWhiteSpace(w.Reps))
            .Select(w => new WorkoutLog(
                Month: dailyFile.Date.Month.ToString("00"),
                Day: dailyFile.Date.Day.ToString("00"),
                Type: w.Exercise,
                Reps: w.Reps
            ));
    }

    public static IEnumerable<ExpenseLog> ToExpenseLogs(this DailyFile dailyFile)
    {
        return dailyFile.Expenses
            .Where(e => e.Amount > 0)
            .Select(e => new ExpenseLog(
                Month: dailyFile.Date.Month,
                Day: dailyFile.Date.Day,
                Category: e.Category,
                Amount: e.Amount,
                Description: e.Description
            ));
    }

    public static IEnumerable<string> ToMarkdownLines(this ExpenseReport report)
    {
        var sections = new List<string>();
        
        foreach (var month in report.Months)
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

    public static IEnumerable<string> ToMarkdownLines(this DailyFile dailyFile)
    {
        var workoutLines = string.Join("\n", dailyFile.Workouts.Select(w => $"{w.Exercise},{w.Reps}"));
        
        var markdown = $"""
            # {DailySectionName.Date}
            {dailyFile.Date:yyyy-MM-dd}

            # {DailySectionName.Workout}
            exercise,reps
            {workoutLines}

            # {DailySectionName.Expenses}
            category,amount,description
            """;

        return markdown.Split('\n');
    }

    public static IEnumerable<DailyWorkout> ParseDailyWorkouts(this IEnumerable<string> csvLines)
    {
        return csvLines
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = line.Split(',');
                return new DailyWorkout(
                    Exercise: parts[0],
                    Reps: parts.Length > 1 ? parts[1] : ""
                );
            });
    }

    public static IEnumerable<DailyExpense> ParseDailyExpenses(this IEnumerable<string> csvLines)
    {
        return csvLines
            .Skip(1) // Skip header
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = line.Split(',');
                return new DailyExpense(
                    Category: parts[0],
                    Amount: parts.Length > 1 && decimal.TryParse(parts[1], out var amt) ? amt : 0,
                    Description: parts.Length > 2 ? parts[2] : ""
                );
            });
    }

    public static IEnumerable<ExpenseLog> ParseExpenseLogs(this IEnumerable<string> csvLines)
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

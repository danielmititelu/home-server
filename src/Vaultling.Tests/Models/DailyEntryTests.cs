using Vaultling.Models;

namespace Vaultling.Tests;

public class DailyEntryTests
{
    private static readonly string TestDataPath = Path.Combine("TestData", "daily-entry.md");

    [Fact]
    public void Parse_ReadsDateCorrectly()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntry.Parse(lines);

        Assert.Equal(2026, entry.Date.Year);
        Assert.Equal(3, entry.Date.Month);
        Assert.Equal(7, entry.Date.Day);
    }

    [Fact]
    public void Parse_ReadsWorkouts()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntry.Parse(lines);
        var workouts = entry.Workouts.ToList();

        Assert.Equal(2, workouts.Count);
        Assert.Equal("pushups", workouts[0].Exercise);
        Assert.Equal("20-20-20", workouts[0].Reps);
        Assert.Equal("squats", workouts[1].Exercise);
        Assert.Equal("20-20-20", workouts[1].Reps);
    }

    [Fact]
    public void Parse_ReadsExpenses()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntry.Parse(lines);
        var expenses = entry.Expenses.ToList();

        Assert.Equal(2, expenses.Count);
        Assert.Equal("food", expenses[0].Category);
        Assert.Equal(45.50m, expenses[0].Amount);
        Assert.Equal("groceries", expenses[0].Description);
        Assert.Equal("transport", expenses[1].Category);
        Assert.Equal(12.00m, expenses[1].Amount);
    }

    [Fact]
    public void Parse_ReadsTodos()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntry.Parse(lines);
        var todos = entry.Todos.ToList();

        Assert.Equal(2, todos.Count);
        Assert.Equal("Buy milk", todos[0]);
        Assert.Equal("[x] Clean kitchen", todos[1]);
    }

    [Fact]
    public void ToWorkoutLogs_ConvertsCorrectly()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntry.Parse(lines);
        var logs = entry.ToWorkoutLogs().ToList();

        Assert.Equal(2, logs.Count);
        Assert.Equal("07", logs[0].Day);
        Assert.Equal("03", logs[0].Month);
        Assert.Equal("pushups", logs[0].Type);
        Assert.Equal("20-20-20", logs[0].Reps);
    }

    [Fact]
    public void ToExpenseLogs_ConvertsCorrectly()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntry.Parse(lines);
        var logs = entry.ToExpenseLogs().ToList();

        Assert.Equal(2, logs.Count);
        Assert.Equal(3, logs[0].Month);
        Assert.Equal(7, logs[0].Day);
        Assert.Equal("food", logs[0].Category);
        Assert.Equal(45.50m, logs[0].Amount);
    }

    [Fact]
    public void ToMarkdownLines_ThenParse_RoundTrips()
    {
        var lines = File.ReadLines(TestDataPath);
        var original = DailyEntry.Parse(lines);

        var markdown = original.ToMarkdownLines();
        var reparsed = DailyEntry.Parse(markdown);

        Assert.Equal(original.Date.Date, reparsed.Date.Date);
        Assert.Equal(original.Workouts.Count(), reparsed.Workouts.Count());
        Assert.Equal(original.Todos.Count(), reparsed.Todos.Count());
    }
}

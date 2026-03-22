using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class DailyEntryRepositoryTests
{
    private static readonly string TestDataPath = Path.Combine("TestData", "daily-entry.md");

    [Fact]
    public void ParseDailyEntry_ReadsDateCorrectly()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntryRepository.ParseDailyEntry(lines);

        Assert.Equal(2026, entry.Date.Year);
        Assert.Equal(3, entry.Date.Month);
        Assert.Equal(7, entry.Date.Day);
    }

    [Fact]
    public void ParseDailyEntry_ReadsWorkouts()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntryRepository.ParseDailyEntry(lines);
        var workouts = entry.Workouts.ToList();

        Assert.Equal(2, workouts.Count);
        Assert.Equal("pushups", workouts[0].Exercise);
        Assert.Equal("20-20-20", workouts[0].Reps);
        Assert.Equal("squats", workouts[1].Exercise);
        Assert.Equal("20-20-20", workouts[1].Reps);
    }

    [Fact]
    public void ParseDailyEntry_ReadsExpenses()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntryRepository.ParseDailyEntry(lines);
        var expenses = entry.Expenses.ToList();

        Assert.Equal(2, expenses.Count);
        Assert.Equal("food", expenses[0].Category);
        Assert.Equal(45.50m, expenses[0].Amount);
        Assert.Equal("groceries", expenses[0].Description);
        Assert.Equal("transport", expenses[1].Category);
        Assert.Equal(12.00m, expenses[1].Amount);
    }

    [Fact]
    public void ParseDailyEntry_ReadsTodos()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntryRepository.ParseDailyEntry(lines);
        var todos = entry.Todos.ToList();

        Assert.Equal(2, todos.Count);
        Assert.Equal("Buy milk", todos[0]);
        Assert.Equal("[x] Clean kitchen", todos[1]);
    }
}

using Vaultling.Models;
using Vaultling.Services;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class DailyEntryServiceTests
{
    private static readonly string TestDataPath = Path.Combine("TestData", "daily-entry.md");

    [Fact]
    public void ToWorkoutLogs_ConvertsCorrectly()
    {
        var lines = File.ReadLines(TestDataPath);
        var entry = DailyEntryRepository.ParseDailyEntry(lines);
        var logs = DailyEntryService.ToWorkoutLogs(entry).ToList();

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
        var entry = DailyEntryRepository.ParseDailyEntry(lines);
        var logs = DailyEntryService.ToExpenseLogs(entry).ToList();

        Assert.Equal(2, logs.Count);
        Assert.Equal(3, logs[0].Month);
        Assert.Equal(7, logs[0].Day);
        Assert.Equal("food", logs[0].Category);
        Assert.Equal(45.50m, logs[0].Amount);
    }

    [Fact]
    public void GenerateMarkdownForDailyEntry_ThenParse_RoundTrips()
    {
        var lines = File.ReadLines(TestDataPath);
        var original = DailyEntryRepository.ParseDailyEntry(lines);

        var markdown = DailyEntryService.GenerateMarkdownForDailyEntry(original);
        var reparsed = DailyEntryRepository.ParseDailyEntry(markdown);

        Assert.Equal(original.Date.Date, reparsed.Date.Date);
        Assert.Equal(original.Workouts.Count(), reparsed.Workouts.Count());
        Assert.Equal(original.Todos.Count(), reparsed.Todos.Count());
    }
}

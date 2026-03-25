using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class DailyEntryServiceTests
{
    private static readonly string TestDataPath = Path.Combine("TestData", "daily-entry.md");

    private static DailyEntry ReadEntryFromFile(string path)
    {
        var repo = new DailyEntryRepository(Options.Create(new DailyEntryOptions
        {
            TodayFile = path,
            HistoryDirectory = Path.GetTempPath()
        }));
        return repo.ReadDailyEntry();
    }

    [Fact]
    public void ToWorkoutLogs_ConvertsCorrectly()
    {
        var entry = ReadEntryFromFile(TestDataPath);
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
        var entry = ReadEntryFromFile(TestDataPath);
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
        var original = ReadEntryFromFile(TestDataPath);

        var markdown = DailyEntryService.GenerateMarkdownForDailyEntry(original);
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, markdown);
        var reparsed = ReadEntryFromFile(tempFile);
        File.Delete(tempFile);

        Assert.Equal(original.Date.Date, reparsed.Date.Date);
        Assert.Equal(original.Workouts.Count(), reparsed.Workouts.Count());
        Assert.Equal(original.Todos.Count(), reparsed.Todos.Count());
    }

    [Fact]
    public void GenerateMarkdownForDailyEntry_UsesRelativeCalendarLabels()
    {
        var entryDate = new DateTimeOffset(2026, 3, 26, 9, 0, 0, TimeSpan.Zero);
        var entry = new DailyEntry(
            Date: entryDate,
            Workouts: [],
            Todos: [],
            Expenses: [],
            CalendarEvents:
            [
                new CalendarOccurrence(new DateTime(2026, 3, 26, 18, 0, 0), "Piano lesson"),
                new CalendarOccurrence(new DateTime(2026, 3, 27, 20, 0, 0), "Movie night"),
                new CalendarOccurrence(new DateTime(2026, 3, 28, 0, 0, 0), "Picnic", true),
                new CalendarOccurrence(new DateTime(2026, 4, 2, 18, 0, 0), "Piano lesson")
            ]);

        var markdown = string.Join("\n", DailyEntryService.GenerateMarkdownForDailyEntry(entry));
        var expectedCalendarLink = Vaultling.Utils.Utils.GetCalendarReportMonthLink(entryDate.DateTime);

        Assert.Contains(expectedCalendarLink, markdown);
        Assert.Contains("- Azi la 18:00: Piano lesson", markdown);
        Assert.Contains("- Mâine la 20:00: Movie night", markdown);
        Assert.Contains("- ~~Sâmbătă: Picnic~~", markdown);
        Assert.Contains("- Joia următoare la 18:00: Piano lesson", markdown);
    }

    [Fact]
    public void GenerateMarkdownForDailyEntry_AddsDefaultTodo_WhenTodosAreEmpty()
    {
        var entry = new DailyEntry(
            Date: new DateTimeOffset(2026, 3, 26, 9, 0, 0, TimeSpan.Zero),
            Workouts: [],
            Todos: [],
            Expenses: [],
            CalendarEvents: []);

        var markdown = string.Join("\n", DailyEntryService.GenerateMarkdownForDailyEntry(entry));

        Assert.Contains("# Todo", markdown);
        Assert.Contains("- [ ]", markdown);
    }
}

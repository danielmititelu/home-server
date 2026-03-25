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
    public void ProcessDailyEntry_AppendsWorkoutAndExpenseLogs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-daily-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var todayFile = Path.Combine(tempDir, "daily-entry.md");
            File.Copy(TestDataPath, todayFile);

            var workoutLog = Path.Combine(tempDir, "workouts.csv");
            File.WriteAllLines(workoutLog, ["month,day,type,reps"]);

            var scheduleFile = Path.Combine(tempDir, "schedule.csv");
            File.WriteAllLines(scheduleFile, ["day,exercise1,exercise2"]);

            var expenseFile = Path.Combine(tempDir, "expenses.csv");
            File.WriteAllLines(expenseFile, ["month,day,category,amount,description"]);

            var service = new DailyEntryService(
                new DailyEntryRepository(Options.Create(new DailyEntryOptions
                {
                    TodayFile = todayFile,
                    HistoryDirectory = tempDir
                })),
                new WorkoutRepository(Options.Create(new WorkoutOptions
                {
                    LogFile = workoutLog,
                    ScheduleFile = scheduleFile
                }), TimeProvider.System),
                new ExpenseRepository(Options.Create(new ExpenseOptions
                {
                    DataFile = expenseFile
                })),
                new CalendarRepository(Options.Create(new CalendarOptions())),
                TimeProvider.System);

            service.ProcessDailyEntry();

            var workouts = new WorkoutRepository(
                Options.Create(new WorkoutOptions { LogFile = workoutLog }),
                TimeProvider.System)
                .ReadWorkoutLogs().ToList();

            Assert.Equal(2, workouts.Count);
            Assert.Equal("03", workouts[0].Month);
            Assert.Equal("07", workouts[0].Day);
            Assert.Equal("pushups", workouts[0].Type);
            Assert.Equal("20-20-20", workouts[0].Reps);

            var expenses = new ExpenseRepository(
                Options.Create(new ExpenseOptions { DataFile = expenseFile }))
                .ReadExpenses().ToList();

            Assert.Equal(2, expenses.Count);
            Assert.Equal(3, expenses[0].Month);
            Assert.Equal(7, expenses[0].Day);
            Assert.Equal("food", expenses[0].Category);
            Assert.Equal(45.50m, expenses[0].Amount);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
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

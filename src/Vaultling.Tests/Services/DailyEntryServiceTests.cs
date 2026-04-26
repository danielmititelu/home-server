using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class DailyEntryServiceTests
{
    private static DailyEntry ReadEntryFromFile(string path)
    {
        var repo = new DailyEntryRepository(Options.Create(new DailyEntryOptions
        {
            TodayFile = path,
            HistoryDirectory = Path.GetTempPath()
        }));
        return repo.ReadDailyEntry();
    }

    private static WeatherRepository CreateStubWeatherRepository() =>
        new WeatherRepository(new HttpClient(new StubHttpHandler()));

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task ProcessDailyEntry_AppendsWorkoutAndExpenseLogs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-daily-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var todayFile = Path.Combine(tempDir, "daily-entry.md");
            File.WriteAllText(todayFile, """
                # Date
                2026-03-07

                # Weather
                Bucharest

                # Workout
                exercise,reps
                pushups,20-20-20
                squats,20-20-20

                # Expenses
                category,amount,description
                food,45.50,groceries
                transport,12.00,bus

                # Todo
                Buy milk
                [x] Clean kitchen
                """);

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
                    CurrentYearLogFile = workoutLog,
                    ScheduleFile = scheduleFile
                }), TimeProvider.System),
                new ExpenseRepository(Options.Create(new ExpenseOptions
                {
                    CurrentYearDataFile = expenseFile
                })),
                new CalendarRepository(
                    Options.Create(new CalendarOptions()),
                    new ExpenseRepository(Options.Create(new ExpenseOptions())),
                    TimeProvider.System),
                CreateStubWeatherRepository(),
                TimeProvider.System);

            await service.ProcessDailyEntryAsync();

            var workouts = new WorkoutRepository(
                Options.Create(new WorkoutOptions { CurrentYearLogFile = workoutLog }),
                TimeProvider.System)
                .ReadWorkoutLogs().ToList();

            Assert.Equal(2, workouts.Count);
            Assert.Equal(3, workouts[0].Month);
            Assert.Equal(7, workouts[0].Day);
            Assert.Equal("pushups", workouts[0].Type);
            Assert.Equal("20-20-20", workouts[0].Reps);

            var expenses = new ExpenseRepository(
                Options.Create(new ExpenseOptions { CurrentYearDataFile = expenseFile }))
                .ReadCurrentYearExpenses().ToList();

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
        var original = new DailyEntry(
            Date: new DateTimeOffset(2026, 3, 7, 0, 0, 0, TimeSpan.Zero),
            Workouts: [new DailyWorkout("pushups", "20-20-20"), new DailyWorkout("squats", "20-20-20")],
            Todos: ["Buy milk", "[x] Clean kitchen"],
            Expenses: [new DailyExpense("food", 45.50m, "groceries"), new DailyExpense("transport", 12.00m, "bus")],
            CalendarEvents: [],
            City: "Bucharest");

        var markdown = DailyEntryService.GenerateMarkdownForDailyEntry(original);
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, markdown);
        var reparsed = ReadEntryFromFile(tempFile);
        File.Delete(tempFile);

        Assert.Equal(original.Date.Date, reparsed.Date.Date);
        Assert.Equal(original.Workouts.Count(), reparsed.Workouts.Count());
        Assert.Equal(original.Todos.Count(), reparsed.Todos.Count());
        Assert.Equal(original.City, reparsed.City);
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

        var markdown = DailyEntryService.GenerateMarkdownForDailyEntry(entry);

        var expectedCalendarLink = Utils.Utils.GetCalendarReportMonthLink(entryDate.DateTime);
        Assert.Contains(expectedCalendarLink, markdown);
        Assert.Contains("Azi la 18:00: Piano lesson", markdown);
        Assert.Contains("Mâine la 20:00: Movie night", markdown);
        Assert.Contains("~~Sâmbătă: Picnic~~", markdown);
        Assert.Contains("Joia următoare la 18:00: Piano lesson", markdown);
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

        var markdown = DailyEntryService.GenerateMarkdownForDailyEntry(entry);

        Assert.Contains("# Todo", markdown);
        Assert.Contains("- [ ]", markdown);
    }

    [Fact]
    public void GenerateMarkdownForDailyEntry_IncludesWeatherInfo_WhenProvided()
    {
        var entry = new DailyEntry(
            Date: new DateTimeOffset(2026, 4, 12, 9, 0, 0, TimeSpan.Zero),
            Workouts: [],
            Todos: [],
            Expenses: [],
            CalendarEvents: [],
            City: "Bucharest");
        var weather = new WeatherInfo("Bucharest", "\u2600\ufe0f 18\u00b0C, Clear sky", "06:15", "19:50");

        var markdown = DailyEntryService.GenerateMarkdownForDailyEntry(entry, weather);

        Assert.Contains("# Weather", markdown);
        Assert.Contains("Bucharest", markdown);
        Assert.Contains("\u2600\ufe0f 18\u00b0C, Clear sky", markdown);
        Assert.Contains("\ud83c\udf05 06:15 \ud83c\udf07 19:50", markdown);
    }

    [Fact]
    public void GenerateMarkdownForDailyEntry_ShowsOnlyCity_WhenWeatherFetchFailed()
    {
        var entry = new DailyEntry(
            Date: new DateTimeOffset(2026, 4, 12, 9, 0, 0, TimeSpan.Zero),
            Workouts: [],
            Todos: [],
            Expenses: [],
            CalendarEvents: [],
            City: "Bucharest");

        var markdown = DailyEntryService.GenerateMarkdownForDailyEntry(entry, weather: null);

        Assert.Contains("# Weather", markdown);
        Assert.Contains("Bucharest", markdown);
        Assert.DoesNotContain("\u00b0C", markdown);
    }

    [Fact]
    public async Task ProcessDailyEntry_WeightedWorkout_NormalizesReps()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-weighted-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var todayFile = Path.Combine(tempDir, "daily-entry.md");
            File.WriteAllText(todayFile, """
                # Date
                2026-03-07

                # Weather
                Bucharest

                # Workout
                exercise,reps
                squats,10x15,10x15,10x15

                # Expenses
                category,amount,description

                # Todo
                - [ ]
                """);

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
                    CurrentYearLogFile = workoutLog,
                    ScheduleFile = scheduleFile
                }), TimeProvider.System),
                new ExpenseRepository(Options.Create(new ExpenseOptions
                {
                    CurrentYearDataFile = expenseFile
                })),
                new CalendarRepository(
                    Options.Create(new CalendarOptions()),
                    new ExpenseRepository(Options.Create(new ExpenseOptions())),
                    TimeProvider.System),
                CreateStubWeatherRepository(),
                TimeProvider.System);

            await service.ProcessDailyEntryAsync();

            var workouts = new WorkoutRepository(
                Options.Create(new WorkoutOptions { CurrentYearLogFile = workoutLog }),
                TimeProvider.System)
                .ReadWorkoutLogs().ToList();

            var log = Assert.Single(workouts);
            Assert.Equal("squats", log.Type);
            Assert.Equal("10x15-10x15-10x15", log.Reps);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

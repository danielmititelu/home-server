using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class DailyEntryRepositoryTests
{
    private static readonly string SampleEntry = """
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
        """;

    private static DailyEntry ReadEntry(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        try
        {
            return new DailyEntryRepository(Options.Create(new DailyEntryOptions
            {
                TodayFile = tempFile,
                HistoryDirectory = Path.GetTempPath()
            })).ReadDailyEntry();
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void ReadDailyEntry_ReadsDateCorrectly()
    {
        var entry = ReadEntry(SampleEntry);

        Assert.Equal(2026, entry.Date.Year);
        Assert.Equal(3, entry.Date.Month);
        Assert.Equal(7, entry.Date.Day);
    }

    [Fact]
    public void ReadDailyEntry_ReadsWorkouts()
    {
        var workouts = ReadEntry(SampleEntry).Workouts.ToList();

        Assert.Equal(2, workouts.Count);
        Assert.Equal("pushups", workouts[0].Exercise);
        Assert.Equal("20-20-20", workouts[0].Reps);
        Assert.Equal("squats", workouts[1].Exercise);
        Assert.Equal("20-20-20", workouts[1].Reps);
    }

    [Fact]
    public void ReadDailyEntry_ReadsExpenses()
    {
        var expenses = ReadEntry(SampleEntry).Expenses.ToList();

        Assert.Equal(2, expenses.Count);
        Assert.Equal("food", expenses[0].Category);
        Assert.Equal(45.50m, expenses[0].Amount);
        Assert.Equal("groceries", expenses[0].Description);
        Assert.Equal("transport", expenses[1].Category);
        Assert.Equal(12.00m, expenses[1].Amount);
    }

    [Fact]
    public void ReadDailyEntry_ReadsTodos()
    {
        var todos = ReadEntry(SampleEntry).Todos.ToList();

        Assert.Equal(2, todos.Count);
        Assert.Equal("Buy milk", todos[0]);
        Assert.Equal("[x] Clean kitchen", todos[1]);
    }
}


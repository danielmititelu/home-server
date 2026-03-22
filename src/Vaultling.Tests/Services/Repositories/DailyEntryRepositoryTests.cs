using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class DailyEntryRepositoryTests
{
    private static readonly string TestDataPath = Path.Combine("TestData", "daily-entry.md");
    private readonly DailyEntryRepository _repository;
    private readonly DailyEntry _entry;

    public DailyEntryRepositoryTests()
    {
        _repository = new DailyEntryRepository(Options.Create(new DailyEntryOptions
        {
            TodayFile = TestDataPath,
            HistoryDirectory = Path.GetTempPath()
        }));
        _entry = _repository.ReadDailyEntry();
    }

    [Fact]
    public void ReadDailyEntry_ReadsDateCorrectly()
    {
        Assert.Equal(2026, _entry.Date.Year);
        Assert.Equal(3, _entry.Date.Month);
        Assert.Equal(7, _entry.Date.Day);
    }

    [Fact]
    public void ReadDailyEntry_ReadsWorkouts()
    {
        var workouts = _entry.Workouts.ToList();

        Assert.Equal(2, workouts.Count);
        Assert.Equal("pushups", workouts[0].Exercise);
        Assert.Equal("20-20-20", workouts[0].Reps);
        Assert.Equal("squats", workouts[1].Exercise);
        Assert.Equal("20-20-20", workouts[1].Reps);
    }

    [Fact]
    public void ReadDailyEntry_ReadsExpenses()
    {
        var expenses = _entry.Expenses.ToList();

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
        var todos = _entry.Todos.ToList();

        Assert.Equal(2, todos.Count);
        Assert.Equal("Buy milk", todos[0]);
        Assert.Equal("[x] Clean kitchen", todos[1]);
    }
}

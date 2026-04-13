using Vaultling.Models;

namespace Vaultling.Tests;

public class DailyEntryTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var entry = new DailyEntry(
            Date: new DateTimeOffset(2026, 3, 7, 8, 0, 0, TimeSpan.Zero),
            Workouts: [new DailyWorkout("pushups", "20-20-20")],
            Todos: ["Buy milk"],
            Expenses: [new DailyExpense("food", 45.50m, "groceries")],
            CalendarEvents: []
        );

        Assert.Equal(2026, entry.Date.Year);
        Assert.Single(entry.Workouts);
        Assert.Single(entry.Todos);
        Assert.Single(entry.Expenses);
    }

    [Fact]
    public void InstancesWithSameValues_HaveEquivalentProperties()
    {
        var first = new DailyEntry(
            Date: new DateTimeOffset(2026, 3, 7, 8, 0, 0, TimeSpan.Zero),
            Workouts: [new DailyWorkout("pushups", "20-20-20")],
            Todos: ["Buy milk"],
            Expenses: [new DailyExpense("food", 45.50m, "groceries")],
            CalendarEvents: []
        );

        var second = new DailyEntry(
            Date: new DateTimeOffset(2026, 3, 7, 8, 0, 0, TimeSpan.Zero),
            Workouts: [new DailyWorkout("pushups", "20-20-20")],
            Todos: ["Buy milk"],
            Expenses: [new DailyExpense("food", 45.50m, "groceries")],
            CalendarEvents: []
        );

        Assert.Equal(first.Date, second.Date);
        Assert.Equal(first.Workouts.ToList(), second.Workouts.ToList());
        Assert.Equal(first.Todos.ToList(), second.Todos.ToList());
        Assert.Equal(first.Expenses.ToList(), second.Expenses.ToList());
        Assert.Equal(first.CalendarEvents?.ToList(), second.CalendarEvents?.ToList());
    }
}

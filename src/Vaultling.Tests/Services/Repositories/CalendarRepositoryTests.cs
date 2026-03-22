using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class CalendarRepositoryTests
{
    [Fact]
    public void ParseRecurringEvents_ParsesWeeklySchedule()
    {
        var lines = new[]
        {
            "schedule,note",
            "thursday at 18:00,Piano lesson"
        };

        var recurring = CalendarRepository.ParseRecurringEvents(lines).Single();

        Assert.Equal("thursday", recurring.Type);
        Assert.Equal("18:00", recurring.Schedule);
        Assert.Equal("Piano lesson", recurring.Note);
    }

    [Fact]
    public void ParseRecurringEvents_ParsesMonthlySchedule()
    {
        var lines = new[]
        {
            "schedule,note",
            "monthly 15,Pay rent"
        };

        var recurring = CalendarRepository.ParseRecurringEvents(lines).Single();

        Assert.Equal("monthly", recurring.Type);
        Assert.Equal("15", recurring.Schedule);
        Assert.Equal("Pay rent", recurring.Note);
    }

    [Fact]
    public void ParseRecurringEvents_ParsesYearlyMonthDaySchedule()
    {
        var lines = new[]
        {
            "schedule,note",
            "03-15,Doctor appointment"
        };

        var recurring = CalendarRepository.ParseRecurringEvents(lines).Single();

        Assert.Equal("march", recurring.Type);
        Assert.Equal("15", recurring.Schedule);
        Assert.Equal("Doctor appointment", recurring.Note);
    }

    [Fact]
    public void GetOccurrences_MonthlySkipsInvalidDates()
    {
        var recurring = new RecurringEvent("monthly", "31", "Month end");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero);

        var occurrences = CalendarRepository.GetOccurrences(recurring, from, to).ToList();

        Assert.Equal(2, occurrences.Count);
        Assert.Equal(new DateTime(2026, 1, 31), occurrences[0].Date);
        Assert.Equal(new DateTime(2026, 3, 31), occurrences[1].Date);
    }
}

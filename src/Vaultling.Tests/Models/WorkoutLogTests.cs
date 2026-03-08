using Vaultling.Models;

namespace Vaultling.Tests;

public class WorkoutLogTests
{
    private static readonly string TestDataPath = Path.Combine("TestData", "workouts.csv");

    [Fact]
    public void Parse_ReadsAllWorkoutRows()
    {
        var lines = File.ReadLines(TestDataPath);
        var logs = WorkoutLog.Parse(lines).ToList();

        Assert.Equal(3, logs.Count);
    }

    [Fact]
    public void Parse_SkipsHeader()
    {
        var lines = File.ReadLines(TestDataPath);
        var first = WorkoutLog.Parse(lines).First();

        Assert.Equal("01", first.Month);
        Assert.Equal("05", first.Day);
    }

    [Fact]
    public void Parse_ParsesFieldsCorrectly()
    {
        var lines = File.ReadLines(TestDataPath);
        var logs = WorkoutLog.Parse(lines).ToList();

        var first = logs[0];
        Assert.Equal("01", first.Month);
        Assert.Equal("05", first.Day);
        Assert.Equal("pushups", first.Type);
        Assert.Equal("20-20-20", first.Reps);

        var last = logs[2];
        Assert.Equal("02", last.Month);
        Assert.Equal("10", last.Day);
        Assert.Equal("pullups", last.Type);
        Assert.Equal("10-10-10", last.Reps);
    }

    [Fact]
    public void ToCsvLine_RoundTrips()
    {
        var lines = File.ReadLines(TestDataPath);
        var log = WorkoutLog.Parse(lines).First();

        var csv = log.ToCsvLine();
        var reparsed = WorkoutLog.Parse(new[] { "header", csv }).Single();

        Assert.Equal(log, reparsed);
    }
}

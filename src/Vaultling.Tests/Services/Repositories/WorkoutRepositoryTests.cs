using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class WorkoutRepositoryTests
{
    private static readonly string TestDataPath = Path.Combine("TestData", "workouts.csv");
    private readonly WorkoutRepository _repository;
    private readonly List<WorkoutLog> _logs;

    public WorkoutRepositoryTests()
    {
        _repository = new WorkoutRepository(
            Options.Create(new WorkoutOptions { LogFile = TestDataPath }),
            TimeProvider.System);
        _logs = _repository.ReadWorkoutLogs().ToList();
    }

    [Fact]
    public void ReadWorkoutLogs_ReadsAllWorkoutRows()
    {
        Assert.Equal(3, _logs.Count);
    }

    [Fact]
    public void ReadWorkoutLogs_SkipsHeader()
    {
        var first = _logs.First();

        Assert.Equal("01", first.Month);
        Assert.Equal("05", first.Day);
    }

    [Fact]
    public void ReadWorkoutLogs_ParsesFieldsCorrectly()
    {
        var first = _logs[0];
        Assert.Equal("01", first.Month);
        Assert.Equal("05", first.Day);
        Assert.Equal("pushups", first.Type);
        Assert.Equal("20-20-20", first.Reps);

        var last = _logs[2];
        Assert.Equal("02", last.Month);
        Assert.Equal("10", last.Day);
        Assert.Equal("pullups", last.Type);
        Assert.Equal("10-10-10", last.Reps);
    }

    [Fact]
    public void ToCsvLine_RoundTrips()
    {
        var log = _logs.First();

        var csv = WorkoutRepository.ToCsvLine(log);
        var parts = csv.Split(',');
        var reparsed = new WorkoutLog(parts[0], parts[1], parts[2], parts[3]);

        Assert.Equal(log, reparsed);
    }
}

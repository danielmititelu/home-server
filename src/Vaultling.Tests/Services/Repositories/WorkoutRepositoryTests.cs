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

        Assert.Equal(1, first.Month);
        Assert.Equal(5, first.Day);
    }

    [Fact]
    public void ReadWorkoutLogs_ParsesFieldsCorrectly()
    {
        var first = _logs[0];
        Assert.Equal(1, first.Month);
        Assert.Equal(5, first.Day);
        Assert.Equal("pushups", first.Type);
        Assert.Equal("20-20-20", first.Reps);

        var last = _logs[2];
        Assert.Equal(2, last.Month);
        Assert.Equal(10, last.Day);
        Assert.Equal("pullups", last.Type);
        Assert.Equal("10-10-10", last.Reps);
    }

    [Fact]
    public void AppendWorkout_ThenReadWorkoutLogs_RoundTrips()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, ["month,day,type,reps"]);

        try
        {
            var repository = new WorkoutRepository(
                Options.Create(new WorkoutOptions { LogFile = tempFile }),
                TimeProvider.System);

            var log = new WorkoutLog(3, 7, "pushups", "20-20-20");
            repository.AppendWorkout([log]);

            var reparsed = repository.ReadWorkoutLogs().Single();
            Assert.Equal(log, reparsed);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class WorkoutRepositoryTests
{
    private static WorkoutRepository CreateRepository(string filePath) =>
        new WorkoutRepository(
            Options.Create(new WorkoutOptions { CurrentYearLogFile = filePath }),
            TimeProvider.System);

    private static string WriteTempCsv(params string[] rows)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, ["month,day,type,reps", .. rows]);
        return tempFile;
    }

    [Fact]
    public void ReadWorkoutLogs_ReadsAllWorkoutRows()
    {
        var tempFile = WriteTempCsv("01,05,pushups,20-20-20", "01,05,squats,20-20-20", "02,10,pullups,10-10-10");
        try
        {
            var logs = CreateRepository(tempFile).ReadWorkoutLogs().ToList();
            Assert.Equal(3, logs.Count);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void ReadWorkoutLogs_SkipsHeader()
    {
        var tempFile = WriteTempCsv("01,05,pushups,20-20-20");
        try
        {
            var first = CreateRepository(tempFile).ReadWorkoutLogs().First();
            Assert.Equal(1, first.Month);
            Assert.Equal(5, first.Day);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void ReadWorkoutLogs_ParsesFieldsCorrectly()
    {
        var tempFile = WriteTempCsv(
            "01,05,pushups,20-20-20",
            "02,10,pullups,10-10-10",
            "02,15,squats,10x15-10x15-10x15");
        try
        {
            var logs = CreateRepository(tempFile).ReadWorkoutLogs().ToList();

            var first = logs[0];
            Assert.Equal(1, first.Month);
            Assert.Equal(5, first.Day);
            Assert.Equal("pushups", first.Type);
            Assert.Equal("20-20-20", first.Reps);

            var second = logs[1];
            Assert.Equal(2, second.Month);
            Assert.Equal(10, second.Day);
            Assert.Equal("pullups", second.Type);
            Assert.Equal("10-10-10", second.Reps);

            var third = logs[2];
            Assert.Equal(2, third.Month);
            Assert.Equal(15, third.Day);
            Assert.Equal("squats", third.Type);
            Assert.Equal("10x15-10x15-10x15", third.Reps);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void AppendWorkout_WeightedWorkout_ThenReadWorkoutLogs_RoundTrips()
    {
        var tempFile = WriteTempCsv();
        try
        {
            var repository = CreateRepository(tempFile);
            var log = new WorkoutLog(3, 7, "squats", "10x15-10x15-10x15");
            repository.AppendWorkout([log]);

            var reparsed = repository.ReadWorkoutLogs().Single();
            Assert.Equal(log, reparsed);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void AppendWorkout_ThenReadWorkoutLogs_RoundTrips()
    {
        var tempFile = WriteTempCsv();
        try
        {
            var repository = CreateRepository(tempFile);
            var log = new WorkoutLog(3, 7, "pushups", "20-20-20");
            repository.AppendWorkout([log]);

            var reparsed = repository.ReadWorkoutLogs().Single();
            Assert.Equal(log, reparsed);
        }
        finally { File.Delete(tempFile); }
    }
}

using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class CalendarRepositoryTests
{
    private static CalendarRepository MakeRepository(string eventsFile, string singleEventsFile = "")
    {
        return new CalendarRepository(Options.Create(new CalendarOptions
        {
            EventsFile = eventsFile,
            SingleEventsFile = singleEventsFile,
            ReportFile = ""
        }));
    }

    [Fact]
    public void ReadCalendarOccurrences_IncludesSingleEventsFileWithYearPlaceholder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var recurringFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(recurringFile, ["schedule,note"]);

            var singleEventsTemplate = Path.Combine(tempDir, "{year}-events-csv.md");
            var singleEvents2026 = Path.Combine(tempDir, "2026-events-csv.md");
            File.WriteAllLines(singleEvents2026, [
                "date,note",
                "03-15T10:00,Doctor appointment"
            ]);

            var occurrences = MakeRepository(recurringFile, singleEventsTemplate)
                .ReadCalendarOccurrences(2026)
                .ToList();

            var only = Assert.Single(occurrences);
            Assert.Equal(new DateTime(2026, 3, 15, 10, 0, 0), only.Date);
            Assert.Equal("Doctor appointment", only.Note);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ReadCalendarOccurrences_ParsesWeeklySchedule()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, [
            "schedule,note",
            "thursday at 18:00,Piano lesson"
        ]);

        var occurrences = MakeRepository(tempFile).ReadCalendarOccurrences(2026).ToList();

        File.Delete(tempFile);

        var first = occurrences.First();
        Assert.True(occurrences.Count > 0);
        Assert.Equal(DayOfWeek.Thursday, first.Date.DayOfWeek);
        Assert.Equal(18, first.Date.Hour);
        Assert.Equal("Piano lesson", first.Note);
    }

    [Fact]
    public void ReadCalendarOccurrences_ParsesMonthlySchedule()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, [
            "schedule,note",
            "monthly 15,Pay rent"
        ]);

        var occurrences = MakeRepository(tempFile).ReadCalendarOccurrences(2026).ToList();

        File.Delete(tempFile);

        Assert.Equal(12, occurrences.Count);
        Assert.All(occurrences, o => Assert.Equal(15, o.Date.Day));
        Assert.All(occurrences, o => Assert.Equal("Pay rent", o.Note));
    }

    [Fact]
    public void ReadCalendarOccurrences_ParsesYearlyMonthDaySchedule()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, [
            "schedule,note",
            "03-15,Doctor appointment"
        ]);

        var occurrences = MakeRepository(tempFile).ReadCalendarOccurrences(2026).ToList();

        File.Delete(tempFile);

        var only = Assert.Single(occurrences);
        Assert.Equal(new DateTime(2026, 3, 15), only.Date);
        Assert.Equal("Doctor appointment", only.Note);
    }

    [Fact]
    public void GetOccurrences_MonthlySkipsInvalidDates()
    {
        var lines = new[]
        {
            "schedule,note",
            "monthly 31,Month end"
        };

        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, lines);
        var occurrences = MakeRepository(tempFile).ReadCalendarOccurrences(2026).ToList();

        File.Delete(tempFile);

        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero);

        var bounded = occurrences
            .Where(o => o.Date >= from.Date && o.Date <= to.Date)
            .ToList();

        Assert.Equal(2, bounded.Count);
        Assert.Equal(new DateTime(2026, 1, 31), bounded[0].Date);
        Assert.Equal(new DateTime(2026, 3, 31), bounded[1].Date);
    }
}

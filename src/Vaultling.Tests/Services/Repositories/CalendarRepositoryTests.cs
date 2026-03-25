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
                "date,note,cancelled",
                "03-15T10:00,Doctor appointment,true"
            ]);

            var occurrences = MakeRepository(recurringFile, singleEventsTemplate)
                .ReadCalendarOccurrences(2026)
                .ToList();

            var only = Assert.Single(occurrences);
            Assert.Equal(new DateTime(2026, 3, 15, 10, 0, 0), only.Date);
            Assert.Equal("Doctor appointment", only.Note);
            Assert.True(only.Cancelled);
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
            "schedule,note,cycle-start,cycle-count",
            "thursday at 18:00,Piano lesson,,"
        ]);

        var occurrences = MakeRepository(tempFile).ReadCalendarOccurrences(2026).ToList();

        File.Delete(tempFile);

        var first = occurrences.First();
        Assert.True(occurrences.Count > 0);
        Assert.Equal(DayOfWeek.Thursday, first.Date.DayOfWeek);
        Assert.Equal(18, first.Date.Hour);
        Assert.Equal("Piano lesson", first.Note);
        Assert.False(first.Cancelled);
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
        Assert.All(occurrences, o => Assert.False(o.Cancelled));
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
        Assert.False(only.Cancelled);
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

    [Fact]
    public void ReadCalendarOccurrences_CancelledSingleEvent_OverridesRecurringAndAllowsReschedule()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var recurringFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(recurringFile, [
                "schedule,note",
                "thursday at 18:00,Piano lesson"
            ]);

            var singleEventsTemplate = Path.Combine(tempDir, "{year}-events-csv.md");
            var singleEvents2026 = Path.Combine(tempDir, "2026-events-csv.md");
            File.WriteAllLines(singleEvents2026, [
                "date,note,cancelled",
                "01-01T18:00,Piano lesson,true",
                "01-03T18:00,Piano lesson,false"
            ]);

            var occurrences = MakeRepository(recurringFile, singleEventsTemplate)
                .ReadCalendarOccurrences(2026)
                .ToList();

            var cancelledSlot = occurrences
                .Where(o => o.Date == new DateTime(2026, 1, 1, 18, 0, 0) && o.Note == "Piano lesson")
                .ToList();

            var rescheduledSlot = occurrences
                .Where(o => o.Date == new DateTime(2026, 1, 3, 18, 0, 0) && o.Note == "Piano lesson")
                .ToList();

            var cancelledOccurrence = Assert.Single(cancelledSlot);
            Assert.True(cancelledOccurrence.Cancelled);

            var rescheduledOccurrence = Assert.Single(rescheduledSlot);
            Assert.False(rescheduledOccurrence.Cancelled);
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
    public void ReadCalendarOccurrences_AppliesCycleNumbering_FromCycleStart()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, [
            "schedule,note,cycle-start,cycle-count",
            "thursday at 18:00,Piano lesson,2026-01-08,4"
        ]);

        var occurrences = MakeRepository(tempFile).ReadCalendarOccurrences(2026).ToList();
        File.Delete(tempFile);

        // Jan 1 is a Thursday before cycle-start → plain note
        var jan1 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 1, 18, 0, 0));
        Assert.Equal("Piano lesson", jan1.Note);

        // Jan 8 is cycle-start → 1/4
        var jan8 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 8, 18, 0, 0));
        Assert.Equal("Piano lesson 1/4", jan8.Note);

        // Jan 15 → 2/4
        var jan15 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 15, 18, 0, 0));
        Assert.Equal("Piano lesson 2/4", jan15.Note);

        // Jan 22 → 3/4
        var jan22 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 22, 18, 0, 0));
        Assert.Equal("Piano lesson 3/4", jan22.Note);

        // Jan 29 → 4/4
        var jan29 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 29, 18, 0, 0));
        Assert.Equal("Piano lesson 4/4", jan29.Note);

        // Feb 5 → wraps to 1/4
        var feb5 = occurrences.Single(o => o.Date == new DateTime(2026, 2, 5, 18, 0, 0));
        Assert.Equal("Piano lesson 1/4", feb5.Note);
    }

    [Fact]
    public void ReadCalendarOccurrences_CycleNumbering_CancelledConsumesSlot()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var recurringFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(recurringFile, [
                "schedule,note,cycle-start,cycle-count",
                "thursday at 18:00,Piano lesson,2026-01-01,4"
            ]);

            var singleEventsTemplate = Path.Combine(tempDir, "{year}-events-csv.md");
            var singleEvents2026 = Path.Combine(tempDir, "2026-events-csv.md");
            File.WriteAllLines(singleEvents2026, [
                "date,note,cancelled",
                "01-08T18:00,Piano lesson,true"
            ]);

            var occurrences = MakeRepository(recurringFile, singleEventsTemplate)
                .ReadCalendarOccurrences(2026)
                .ToList();

            // Jan 1 → 1/4
            var jan1 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 1, 18, 0, 0));
            Assert.Equal("Piano lesson 1/4", jan1.Note);
            Assert.False(jan1.Cancelled);

            // Jan 8 → 2/4 but cancelled
            var jan8 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 8, 18, 0, 0));
            Assert.Equal("Piano lesson 2/4", jan8.Note);
            Assert.True(jan8.Cancelled);

            // Jan 15 → 3/4 (cancelled consumed the 2/4 slot)
            var jan15 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 15, 18, 0, 0));
            Assert.Equal("Piano lesson 3/4", jan15.Note);
            Assert.False(jan15.Cancelled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

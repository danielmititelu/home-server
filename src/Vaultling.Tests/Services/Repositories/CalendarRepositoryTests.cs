using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class CalendarRepositoryTests
{
    private static CalendarRepository MakeRepository(string eventsFile, string reportFile = "", string expenseDataFile = "")
    {
        return new CalendarRepository(Options.Create(new CalendarOptions
        {
            EventsFile = eventsFile,
            ReportFile = reportFile,
            ExpenseDataFile = expenseDataFile
        }));
    }

    [Fact]
    public void ReadCalendarOccurrences_IncludesUserAddedEventsFromReport()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var recurringFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(recurringFile, ["schedule,note"]);

            var reportFile = Path.Combine(tempDir, "2026-calendar-report.md");
            File.WriteAllLines(reportFile, [
                "## 03 - March",
                "",
                "- 15 at 10:00: Doctor appointment"
            ]);

            var occurrences = MakeRepository(recurringFile, Path.Combine(tempDir, "{year}-calendar-report.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            var only = Assert.Single(occurrences);
            Assert.Equal(new DateTime(2026, 3, 15, 10, 0, 0), only.Date);
            Assert.Equal("Doctor appointment", only.Note);
            Assert.False(only.Cancelled);
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
    public void ReadCalendarOccurrences_CancelledReportEvent_OverridesRecurringAndAllowsReschedule()
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

            var reportFile = Path.Combine(tempDir, "2026-calendar-report.md");
            File.WriteAllLines(reportFile, [
                "## 01 - January",
                "",
                "- ~~01 at 18:00: Piano lesson~~",
                "- 03 at 18:00: Piano lesson"
            ]);

            var occurrences = MakeRepository(recurringFile, Path.Combine(tempDir, "{year}-calendar-report.md"))
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
    public void ReadCalendarOccurrences_ExpenseAnchored_GeneratesCountPlusSpeculative()
    {
        // March 12 2026 is a Thursday
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, [
                "schedule,note,cycle-count,cycle-expense",
                "thursday at 18:00,Piano lesson,4,hobby:pian"
            ]);

            var expenseFile = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile, [
                "month,day,category,amount,description",
                "3,12,hobby,400,pian"
            ]);

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            // 4 numbered + 1 speculative = 5 total
            Assert.Equal(5, occurrences.Count);

            Assert.Equal(new DateTime(2026, 3, 12, 18, 0, 0), occurrences[0].Date);
            Assert.Equal("Piano lesson 1/4", occurrences[0].Note);

            Assert.Equal(new DateTime(2026, 3, 19, 18, 0, 0), occurrences[1].Date);
            Assert.Equal("Piano lesson 2/4", occurrences[1].Note);

            Assert.Equal(new DateTime(2026, 3, 26, 18, 0, 0), occurrences[2].Date);
            Assert.Equal("Piano lesson 3/4", occurrences[2].Note);

            Assert.Equal(new DateTime(2026, 4, 2, 18, 0, 0), occurrences[3].Date);
            Assert.Equal("Piano lesson 4/4", occurrences[3].Note);

            // Speculative: next cycle start, numbered 1/4
            Assert.Equal(new DateTime(2026, 4, 9, 18, 0, 0), occurrences[4].Date);
            Assert.Equal("Piano lesson 1/4", occurrences[4].Note);

            Assert.All(occurrences, o => Assert.False(o.Cancelled));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadCalendarOccurrences_NoMatchingExpense_NoOccurrences()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, [
                "schedule,note,cycle-count,cycle-expense",
                "thursday at 18:00,Piano lesson,4,hobby:pian",
                "monthly 15,Pay rent"
            ]);

            var expenseFile = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile, [
                "month,day,category,amount,description",
                "3,12,food,50,groceries"  // no match for hobby:pian
            ]);

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            // No piano lessons; only monthly Pay rent events
            Assert.DoesNotContain(occurrences, o => o.Note.Contains("Piano"));
            Assert.Equal(12, occurrences.Count(o => o.Note == "Pay rent"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadCalendarOccurrences_MultipleExpenses_UsesLatest()
    {
        // April 9 2026 is a Thursday
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, [
                "schedule,note,cycle-count,cycle-expense",
                "thursday at 18:00,Piano lesson,4,hobby:pian"
            ]);

            var expenseFile = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile, [
                "month,day,category,amount,description",
                "3,12,hobby,400,pian",   // March 12
                "4,9,hobby,400,pian"     // April 9 – latest
            ]);

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            // Cycle should start from April 9 (latest expense), not March 12
            Assert.DoesNotContain(occurrences, o => o.Date < new DateTime(2026, 4, 9));
            Assert.Equal(new DateTime(2026, 4, 9, 18, 0, 0), occurrences[0].Date);
            Assert.Equal("Piano lesson 1/4", occurrences[0].Note);
            Assert.Equal(5, occurrences.Count); // 4 + 1 speculative
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadCalendarOccurrences_CancelledSpeculative_StaysCancelled()
    {
        // March 12 2026 is a Thursday; speculative 1/4 falls on April 9
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, [
                "schedule,note,cycle-count,cycle-expense",
                "thursday at 18:00,Piano lesson,4,hobby:pian"
            ]);

            var expenseFile = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile, [
                "month,day,category,amount,description",
                "3,12,hobby,400,pian"
            ]);

            var reportFile = Path.Combine(tempDir, "2026-calendar-report.md");
            File.WriteAllLines(reportFile, [
                "## 04 - April",
                "",
                "- ~~09 at 18:00: Piano lesson 1/4~~"
            ]);

            var occurrences = MakeRepository(eventsFile, Path.Combine(tempDir, "{year}-calendar-report.md"), Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            var speculative = occurrences.Single(o => o.Date == new DateTime(2026, 4, 9, 18, 0, 0));
            Assert.Equal("Piano lesson 1/4", speculative.Note);
            Assert.True(speculative.Cancelled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadCalendarOccurrences_ExpenseMatchesCategoryAndDescription()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, [
                "schedule,note,cycle-count,cycle-expense",
                "thursday at 18:00,Piano lesson,4,hobby:pian"
            ]);

            // Wrong category
            var wrongCategoryExpense = Path.Combine(tempDir, "wrong-cat.csv");
            File.WriteAllLines(wrongCategoryExpense, [
                "month,day,category,amount,description",
                "3,12,food,400,pian"
            ]);
            Assert.DoesNotContain("Piano", MakeRepository(eventsFile, expenseDataFile: wrongCategoryExpense).ReadCalendarOccurrences(2026)
                .Select(o => o.Note));

            // Wrong description
            var wrongDescExpense = Path.Combine(tempDir, "wrong-desc.csv");
            File.WriteAllLines(wrongDescExpense, [
                "month,day,category,amount,description",
                "3,12,hobby,400,guitar"
            ]);
            Assert.DoesNotContain("Piano", MakeRepository(eventsFile, expenseDataFile: wrongDescExpense).ReadCalendarOccurrences(2026)
                .Select(o => o.Note));

            // Correct match (case-insensitive, partial)
            var correctExpense = Path.Combine(tempDir, "correct.csv");
            File.WriteAllLines(correctExpense, [
                "month,day,category,amount,description",
                "3,12,Hobby,400,Piano lessons"
            ]);
            Assert.Contains(MakeRepository(eventsFile, expenseDataFile: correctExpense).ReadCalendarOccurrences(2026),
                o => o.Note.Contains("Piano"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("Piano lesson 1/4", "Piano lesson")]
    [InlineData("Piano lesson 4/4", "Piano lesson")]
    [InlineData("Piano lesson 10/12", "Piano lesson")]
    [InlineData("Piano lesson", "Piano lesson")]
    [InlineData("Pay rent", "Pay rent")]
    public void StripCycleNumber_RemovesTrailingXOfN(string input, string expected)
    {
        Assert.Equal(expected, CalendarRepository.StripCycleNumber(input));
    }

    [Fact]
    public void ReadCalendarOccurrences_CycleNumbering_CancelledInReportConsumesSlot()
    {
        // Jan 1 2026 is a Thursday; expense on Jan 1 starts the cycle
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, [
                "schedule,note,cycle-count,cycle-expense",
                "thursday at 18:00,Piano lesson,4,hobby:pian"
            ]);

            var expenseFile = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile, [
                "month,day,category,amount,description",
                "1,1,hobby,400,pian"
            ]);

            var reportFile = Path.Combine(tempDir, "2026-calendar-report.md");
            File.WriteAllLines(reportFile, [
                "## 01 - January",
                "",
                "- 01 at 18:00: Piano lesson 1/4",
                "- ~~08 at 18:00: Piano lesson 2/4~~",
                "- 15 at 18:00: Piano lesson 3/4"
            ]);

            var occurrences = MakeRepository(eventsFile, Path.Combine(tempDir, "{year}-calendar-report.md"), Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            var jan1 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 1, 18, 0, 0));
            Assert.Equal("Piano lesson 1/4", jan1.Note);
            Assert.False(jan1.Cancelled);

            // Jan 8 (2/4) cancelled via strikethrough — slot still consumed
            var jan8 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 8, 18, 0, 0));
            Assert.Equal("Piano lesson 2/4", jan8.Note);
            Assert.True(jan8.Cancelled);

            // Jan 15 is still 3/4
            var jan15 = occurrences.Single(o => o.Date == new DateTime(2026, 1, 15, 18, 0, 0));
            Assert.Equal("Piano lesson 3/4", jan15.Note);
            Assert.False(jan15.Cancelled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseReportEventLine_AllDayEvent_HasMidnightTime()
    {
        var occurrence = CalendarRepository.ParseReportEventLine("- 15: Nancy birthday", 2026, 3);

        Assert.NotNull(occurrence);
        Assert.Equal(new DateTime(2026, 3, 15), occurrence!.Date);
        Assert.Equal(TimeSpan.Zero, occurrence.Date.TimeOfDay);
        Assert.Equal("Nancy birthday", occurrence.Note);
        Assert.False(occurrence.Cancelled);
    }

    [Fact]
    public void ReadCalendarOccurrences_ReportAllDayEvent_IsIncluded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var recurringFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(recurringFile, ["schedule,note"]);

            var reportFile = Path.Combine(tempDir, "2026-calendar-report.md");
            File.WriteAllLines(reportFile, [
                "## 03 - March",
                "",
                "- 15: Nancy birthday"
            ]);

            var occurrences = MakeRepository(recurringFile, Path.Combine(tempDir, "{year}-calendar-report.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            var only = Assert.Single(occurrences);
            Assert.Equal(new DateTime(2026, 3, 15), only.Date);
            Assert.Equal(TimeSpan.Zero, only.Date.TimeOfDay);
            Assert.Equal("Nancy birthday", only.Note);
            Assert.False(only.Cancelled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetCycleOccurrences_GeneratesExactlyCountPlusOneSpeculative()
    {
        // Jan 1 2026 is a Thursday
        var recurring = new RecurringEvent(Type: "thursday", Schedule: "18:00", Note: "Piano lesson", CycleCount: 4, CycleExpenseMatch: "hobby:pian");
        var occurrences = CalendarRepository.GetCycleOccurrences(recurring, new DateTime(2026, 1, 1)).ToList();

        Assert.Equal(5, occurrences.Count);
        Assert.Equal("Piano lesson 1/4", occurrences[0].Note);
        Assert.Equal("Piano lesson 2/4", occurrences[1].Note);
        Assert.Equal("Piano lesson 3/4", occurrences[2].Note);
        Assert.Equal("Piano lesson 4/4", occurrences[3].Note);
        Assert.Equal("Piano lesson 1/4", occurrences[4].Note); // speculative

        Assert.Equal(new DateTime(2026, 1, 1, 18, 0, 0), occurrences[0].Date);
        Assert.Equal(new DateTime(2026, 1, 8, 18, 0, 0), occurrences[1].Date);
        Assert.Equal(new DateTime(2026, 1, 15, 18, 0, 0), occurrences[2].Date);
        Assert.Equal(new DateTime(2026, 1, 22, 18, 0, 0), occurrences[3].Date);
        Assert.Equal(new DateTime(2026, 1, 29, 18, 0, 0), occurrences[4].Date);
    }

    [Theory]
    [InlineData("Concert metalica pe 2026-06-20T20:00", "Concert metalica", 2026, 6, 20, 20, 0)]
    [InlineData("Metalica concert at 2026-06-20T20:00", "Metalica concert", 2026, 6, 20, 20, 0)]
    [InlineData("Concert metalica pe 2026-06-20", "Concert metalica", 2026, 6, 20, 0, 0)]
    [InlineData("Some event on 2026-03-15T09:30", "Some event", 2026, 3, 15, 9, 30)]
    [InlineData("Party in 2026-12-31", "Party", 2026, 12, 31, 0, 0)]
    [InlineData("Event la 2026-07-04T18:00", "Event", 2026, 7, 4, 18, 0)]
    public void ReadExpenseEvents_ParsesDateFromDescription(
        string description, string expectedNote,
        int yr, int mo, int day, int hr, int min)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, ["schedule,note"]);

            var expenseFile = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile, [
                "month,day,category,amount,description",
                $"4,1,fun,200,{description}"
            ]);

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            var evt = Assert.Single(occurrences);
            Assert.Equal(expectedNote, evt.Note);
            Assert.Equal(new DateTime(yr, mo, day, hr, min, 0), evt.Date);
            Assert.False(evt.Cancelled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadExpenseEvents_NoDateInDescription_NoEvent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, ["schedule,note"]);

            var expenseFile = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile, [
                "month,day,category,amount,description",
                "4,1,fun,200,Concert metalica"
            ]);

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            Assert.Empty(occurrences);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadExpenseEvents_PreviousYearExpenseWithFutureDateAppearsInTargetYear()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, ["schedule,note"]);

            // Expense logged in 2025 file, but event date is 2026
            var expenseFile2025 = Path.Combine(tempDir, "2025-expenses-csv.md");
            File.WriteAllLines(expenseFile2025, [
                "month,day,category,amount,description",
                "12,15,fun,300,Metalica concert at 2026-06-20T20:00"
            ]);
            // No 2026 expense file
            var expenseFile2026 = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile2026, ["month,day,category,amount,description"]);

            var occurrences2025 = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2025)
                .ToList();
            var occurrences2026 = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            // Event in 2026 should appear in 2026 report, not 2025
            Assert.Empty(occurrences2025);
            var evt = Assert.Single(occurrences2026);
            Assert.Equal("Metalica concert", evt.Note);
            Assert.Equal(new DateTime(2026, 6, 20, 20, 0, 0), evt.Date);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadExpenseEvents_CancellableViaReportStrikethrough()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vaultling-calendar-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var eventsFile = Path.Combine(tempDir, "events-csv.md");
            File.WriteAllLines(eventsFile, ["schedule,note"]);

            var expenseFile = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile, [
                "month,day,category,amount,description",
                "4,1,fun,200,Concert metalica pe 2026-06-20T20:00"
            ]);

            var reportFile = Path.Combine(tempDir, "2026-calendar-report.md");
            File.WriteAllLines(reportFile, [
                "## 06 - June",
                "",
                "- ~~20 at 20:00: Concert metalica~~"
            ]);

            var occurrences = MakeRepository(eventsFile, Path.Combine(tempDir, "{year}-calendar-report.md"), Path.Combine(tempDir, "{year}-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            var evt = Assert.Single(occurrences);
            Assert.Equal("Concert metalica", evt.Note);
            Assert.True(evt.Cancelled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

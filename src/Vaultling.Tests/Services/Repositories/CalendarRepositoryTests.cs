using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Tests;

public class CalendarRepositoryTests
{
    private static ExpenseRepository MakeExpenseRepository(string dataFile = "", string previousYearDataFile = "")
        => new(Options.Create(new ExpenseOptions { DataFile = dataFile, PreviousYearDataFile = previousYearDataFile }));

    private static CalendarRepository MakeRepository(string eventsFile, string reportFile = "", string expenseDataFile = "")
        => MakeRepository(eventsFile, reportFile, MakeExpenseRepository(expenseDataFile));

    private static CalendarRepository MakeRepository(string eventsFile, ExpenseRepository expenseRepository)
        => MakeRepository(eventsFile, "", expenseRepository);

    private static CalendarRepository MakeRepository(string eventsFile, string reportFile, ExpenseRepository expenseRepository)
    {
        return new CalendarRepository(Options.Create(new CalendarOptions
        {
            EventsFile = eventsFile,
            ReportFile = reportFile,
        }), expenseRepository);
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

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "2026-expenses-csv.md"))
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
                "thursday at 18:00,Piano lesson,4,hobby:pian"
            ]);

            var expenseFile = Path.Combine(tempDir, "2026-expenses-csv.md");
            File.WriteAllLines(expenseFile, [
                "month,day,category,amount,description",
                "3,12,food,50,groceries"  // no match for hobby:pian
            ]);

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "2026-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .ToList();

            // No piano lessons when expense doesn't match
            Assert.Empty(occurrences);
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

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "2026-expenses-csv.md"))
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

            var occurrences = MakeRepository(eventsFile, Path.Combine(tempDir, "{year}-calendar-report.md"), MakeExpenseRepository(dataFile: Path.Combine(tempDir, "2026-expenses-csv.md")))
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
        var recurring = new WeeklyRecurringEvent(DayOfWeek.Thursday, new TimeOnly(18, 0), "Piano lesson", CycleCount: 4, CycleExpenseCategory: "hobby", CycleExpenseDesc: "pian");
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

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "2026-expenses-csv.md"))
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

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "2026-expenses-csv.md"))
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

            var occurrences2025 = MakeRepository(eventsFile, MakeExpenseRepository(dataFile: expenseFile2025))
                .ReadCalendarOccurrences(2025)
                .ToList();
            var occurrences2026 = MakeRepository(eventsFile, MakeExpenseRepository(dataFile: expenseFile2026, previousYearDataFile: expenseFile2025))
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

            var occurrences = MakeRepository(eventsFile, Path.Combine(tempDir, "{year}-calendar-report.md"), MakeExpenseRepository(dataFile: Path.Combine(tempDir, "2026-expenses-csv.md")))
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

    [Theory]
    [InlineData("Avion spre Vienna 2026-06-05T08:00 -> 2026-06-10T20:00", "Avion spre Vienna", 2026, 6, 5, 8, 0, 2026, 6, 10, 20, 0)]
    [InlineData("Flight to Paris 2026-03-01T06:00 -> 2026-03-07T19:00", "Flight to Paris", 2026, 3, 1, 6, 0, 2026, 3, 7, 19, 0)]
    public void ReadExpenseEvents_RangedDate_TwoEventsWithSameNote(
        string description,
        string expectedNote,
        int yr1, int mo1, int d1, int h1, int min1,
        int yr2, int mo2, int d2, int h2, int min2)
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
                $"1,15,transport,600,{description}"
            ]);

            var occurrences = MakeRepository(eventsFile, expenseDataFile: Path.Combine(tempDir, "2026-expenses-csv.md"))
                .ReadCalendarOccurrences(2026)
                .OrderBy(o => o.Date)
                .ToList();

            Assert.Equal(2, occurrences.Count);
            Assert.All(occurrences, o => Assert.Equal(expectedNote, o.Note));
            Assert.Equal(new DateTime(yr1, mo1, d1, h1, min1, 0), occurrences[0].Date);
            Assert.Equal(new DateTime(yr2, mo2, d2, h2, min2, 0), occurrences[1].Date);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetTravelCityForDate_InsideWindow_ReturnsTravelCity()
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
                "1,15,transport,600,Avion spre Vienna 2026-06-05T08:00 -> 2026-06-10T20:00"
            ]);

            var repo = MakeRepository(eventsFile, MakeExpenseRepository(dataFile: Path.Combine(tempDir, "2026-expenses-csv.md")));
            // Departure day — in Vienna
            Assert.Equal("Vienna", repo.GetTravelCityForDate(new DateTime(2026, 6, 5)));
            // Middle of trip
            Assert.Equal("Vienna", repo.GetTravelCityForDate(new DateTime(2026, 6, 8)));
            // Day before return (last day away)
            Assert.Equal("Vienna", repo.GetTravelCityForDate(new DateTime(2026, 6, 9)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetTravelCityForDate_OnReturnDayAndOutside_ReturnsNull()
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
                "1,15,transport,600,Avion spre Vienna 2026-06-05T08:00 -> 2026-06-10T20:00"
            ]);

            var repo = MakeRepository(eventsFile, MakeExpenseRepository(dataFile: Path.Combine(tempDir, "2026-expenses-csv.md")));
            // Return day — back home
            Assert.Null(repo.GetTravelCityForDate(new DateTime(2026, 6, 10)));
            // Before departure
            Assert.Null(repo.GetTravelCityForDate(new DateTime(2026, 6, 4)));
            // After return
            Assert.Null(repo.GetTravelCityForDate(new DateTime(2026, 6, 11)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetTravelCityForDate_NoExpenseFile_ReturnsNull()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, ["schedule,note"]);
        var repo = MakeRepository(tempFile, expenseDataFile: "");

        Assert.Null(repo.GetTravelCityForDate(new DateTime(2026, 6, 7)));

        File.Delete(tempFile);
    }
}

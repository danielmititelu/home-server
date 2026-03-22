namespace Vaultling.Models;

using System.Globalization;

public record CalendarEvent(string Date, string Note)
{
    public static IEnumerable<CalendarEvent> Parse(IEnumerable<string> csvLines)
    {
        return csvLines
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = line.Split(',', 2);
                return new CalendarEvent(
                    Date: parts[0].Trim(),
                    Note: parts[1].Trim()
                );
            });
    }

    public CalendarOccurrence ToOccurrence(int year)
    {
        var dt = DateTimeOffset.Parse($"{year}-{Date}");
        return new CalendarOccurrence(dt.DateTime, Note);
    }

    public string ToCsvLine() => $"{Date},{Note}";
}

public record RecurringEvent(string Type, string Schedule, string Note)
{
    private static readonly string[] MonthNames = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames
        .Where(m => !string.IsNullOrEmpty(m))
        .Select(m => m.ToLower())
        .ToArray();

    private static readonly string[] DayNames = CultureInfo.InvariantCulture.DateTimeFormat.DayNames
        .Select(d => d.ToLower())
        .ToArray();

    public static IEnumerable<RecurringEvent> Parse(IEnumerable<string> csvLines)
    {
        return csvLines
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = line.Split(',', 2);
                var schedule = parts[0].Trim().ToLower();
                var note = parts[1].Trim();
                return ParseSchedule(schedule, note);
            });
    }

    private static RecurringEvent ParseSchedule(string schedule, string note)
    {
        var atIndex = schedule.IndexOf(" at ");
        if (atIndex >= 0)
        {
            var dayName = schedule[..atIndex].Trim();
            var time = schedule[(atIndex + 4)..].Trim();
            if (Array.Exists(DayNames, d => d == dayName))
                return new RecurringEvent(Type: dayName, Schedule: time, Note: note);
        }

        if (schedule.StartsWith("monthly "))
            return new RecurringEvent(Type: "monthly", Schedule: schedule[8..].Trim(), Note: note);

        if (schedule.Length >= 5 && schedule[2] == '-')
        {
            var month = int.Parse(schedule[..2]);
            var day = schedule[3..];
            return new RecurringEvent(Type: MonthNames[month - 1], Schedule: day, Note: note);
        }

        return new RecurringEvent(Type: schedule, Schedule: "", Note: note);
    }

    public IEnumerable<CalendarOccurrence> GetOccurrences(DateTimeOffset from, DateTimeOffset to)
    {
        var type = Type.ToLower();

        if (type == "monthly")
            return GetMonthlyOccurrences(from, to);

        if (Array.Exists(MonthNames, m => m == type))
            return GetYearlyOccurrences(from, to);

        if (Array.Exists(DayNames, d => d == type))
            return GetWeeklyOccurrences(from, to);

        return [];
    }

    private IEnumerable<CalendarOccurrence> GetYearlyOccurrences(DateTimeOffset from, DateTimeOffset to)
    {
        var monthIndex = Array.FindIndex(MonthNames, m => m == Type.ToLower()) + 1;
        var day = int.Parse(Schedule);

        for (var year = from.Year; year <= to.Year; year++)
        {
            var dt = new DateTime(year, monthIndex, day);
            if (dt >= from.Date && dt <= to.Date)
                yield return new CalendarOccurrence(dt, Note);
        }
    }

    private IEnumerable<CalendarOccurrence> GetWeeklyOccurrences(DateTimeOffset from, DateTimeOffset to)
    {
        var targetDay = (DayOfWeek)Array.FindIndex(DayNames, d => d == Type.ToLower());
        var timeParts = Schedule.Split(':');
        var hour = int.Parse(timeParts[0]);
        var minute = int.Parse(timeParts[1]);

        var current = from.Date;
        while (current <= to.Date)
        {
            if (current.DayOfWeek == targetDay)
                yield return new CalendarOccurrence(current.Add(new TimeSpan(hour, minute, 0)), Note);
            current = current.AddDays(1);
        }
    }

    private IEnumerable<CalendarOccurrence> GetMonthlyOccurrences(DateTimeOffset from, DateTimeOffset to)
    {
        var day = int.Parse(Schedule);

        var current = new DateTime(from.Year, from.Month, 1);
        var end = new DateTime(to.Year, to.Month, 1).AddMonths(1);

        while (current < end)
        {
            var daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
            if (day <= daysInMonth)
            {
                var dt = new DateTime(current.Year, current.Month, day);
                if (dt >= from.Date && dt <= to.Date)
                    yield return new CalendarOccurrence(dt, Note);
            }
            current = current.AddMonths(1);
        }
    }
}

public record CalendarOccurrence(DateTime Date, string Note)
{
    public string ToCsvField()
    {
        var monthDay = $"{Date.Month:00}-{Date.Day:00}";
        return Date.TimeOfDay == TimeSpan.Zero
            ? monthDay
            : $"{monthDay}T{Date:HH:mm}";
    }

    public string ToCsvLine() => $"{ToCsvField()},{Note}";

    public string ToMarkdownLine(DateOnly referenceDate)
    {
        var eventDate = DateOnly.FromDateTime(Date);
        var daysDiff = eventDate.DayNumber - referenceDate.DayNumber;

        var dateStr = daysDiff switch
        {
            0 => "Today",
            1 => "Tomorrow",
            _ => eventDate.ToString("dddd")
        };

        var timeStr = Date.TimeOfDay == TimeSpan.Zero
            ? ""
            : $" at {Date:HH:mm}";

        return $"- {dateStr}{timeStr}: {Note}";
    }
}

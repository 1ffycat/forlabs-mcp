using System.Globalization;

namespace ForlabsMcp;

/// <summary>
/// Forlabs encodes its two-week rotating schedule as a single "day" number 1..14:
/// 1-7 = Mon..Sun of the lower week; 8-14 = Mon..Sun of the upper week. The API does not
/// expose a date-to-rotation mapping, so this implementation uses ISO week parity: odd weeks
/// map to the lower block and even weeks to the upper block. This matches the live observations
/// from 2026-09-23 (lower) and 2026-11-26 (upper), but remains an explicit assumption.
/// </summary>
public static class ScheduleMath
{
    /// <summary>Monday-based weekday number: Monday=1 .. Sunday=7.</summary>
    public static int IsoWeekday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(IsoWeekday(date) - 1));

    public static readonly string WeekParityBasis =
        "ISO calendar week parity: odd weeks use lower week (days 1-7), even weeks use upper week (days 8-14); Forlabs does not expose the calendar-to-rotation mapping.";

    public static string WeekType(DateOnly date) =>
        ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)) % 2 == 0 ? "upper" : "lower";

    /// <summary>Resolves the 1..14 day index Forlabs uses for the target date.</summary>
    public static int ResolveDayIndex(DateOnly target)
    {
        var isoWeek = ISOWeek.GetWeekOfYear(target.ToDateTime(TimeOnly.MinValue));
        var offset = isoWeek % 2 == 0 ? 7 : 0;
        return IsoWeekday(target) + offset;
    }

    public static readonly string[] WeekdayRu =
        ["", "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье"];
}

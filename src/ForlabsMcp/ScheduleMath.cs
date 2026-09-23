namespace ForlabsMcp;

/// <summary>
/// Forlabs encodes its two-week rotating schedule as a single "day" number 1..14:
/// 1-7 = Mon..Sun of the calendar week containing "now" at the moment sched/get_schedule
/// was called, 8-14 = Mon..Sun of the following calendar week. (sched/get_grid's "upperweek"
/// field looks like it should tell you this directly, but empirically it does not track which
/// half is "now" — verified live: on 2026-09-23 with upperweek=2, day-range 1-7 held the
/// current week [confirmed empty that Wednesday] and day-range 8-14 held the *next* week's
/// classes, i.e. the opposite of what the field's name suggests. So this class ignores it
/// entirely and anchors day-range 1-7 to "today" directly, alternating by week-parity for
/// dates further out — the two-week rotation is assumed to keep repeating from there.)
/// </summary>
public static class ScheduleMath
{
    /// <summary>Monday-based weekday number: Monday=1 .. Sunday=7.</summary>
    public static int IsoWeekday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(IsoWeekday(date) - 1));

    /// <summary>
    /// Resolves the 1..14 "day" index Forlabs uses for <paramref name="target"/>, given that
    /// <paramref name="today"/> is the date the schedule was fetched for (so it anchors day 1-7
    /// to today's calendar week).
    /// </summary>
    public static int ResolveDayIndex(DateOnly target, DateOnly today)
    {
        var weekDelta = (MondayOf(target).DayNumber - MondayOf(today).DayNumber) / 7;
        var mod = ((weekDelta % 2) + 2) % 2; // 0 = same week-parity as today, 1 = the other
        var offset = mod == 0 ? 0 : 7;
        return IsoWeekday(target) + offset;
    }

    public static readonly string[] WeekdayRu =
        ["", "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье"];
}

namespace ForlabsMcp;

/// <summary>
/// Forlabs encodes the two-week rotating schedule as a single "day" number 1..14:
/// 1-7 = Mon..Sun of one week variant, 8-14 = Mon..Sun of the other. Which half is
/// the "upper" (numerator) week right now is reported live by sched/get_grid as
/// "upperweek" (1 or 2, matching the two halves 1-7 / 8-14). Since that field is
/// evaluated by the server for "now", we anchor it to today's date and derive any
/// other date's day-index from the parity of the ISO week distance to today.
/// </summary>
public static class ScheduleMath
{
    /// <summary>Monday-based weekday number: Monday=1 .. Sunday=7.</summary>
    public static int IsoWeekday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(IsoWeekday(date) - 1));

    /// <summary>
    /// Resolves the 1..14 "day" index Forlabs uses for <paramref name="target"/>,
    /// given that as of <paramref name="today"/> the live grid reported
    /// <paramref name="upperWeekAtToday"/> (1 or 2, selecting the 1-7 / 8-14 half
    /// that applies to *this* calendar week).
    /// </summary>
    public static int ResolveDayIndex(DateOnly target, DateOnly today, int upperWeekAtToday)
    {
        var weekDelta = (MondayOf(target).DayNumber - MondayOf(today).DayNumber) / 7;
        var sameHalfAsToday = weekDelta % 2 == 0; // even distance => same half, odd => the other half
        var half = sameHalfAsToday ? upperWeekAtToday : (upperWeekAtToday == 1 ? 2 : 1);
        var offset = half == 2 ? 7 : 0;
        return IsoWeekday(target) + offset;
    }

    public static readonly string[] WeekdayRu =
        ["", "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье"];
}

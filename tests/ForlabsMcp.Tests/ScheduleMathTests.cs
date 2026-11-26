namespace ForlabsMcp.Tests;

public sealed class ScheduleMathTests
{
    [Test]
    [Arguments(2026, 9, 23, 3, "lower")]
    [Arguments(2026, 11, 26, 11, "upper")]
    public async Task ResolveDayIndexUsesExpectedRotation(
        int year,
        int month,
        int day,
        int expectedDayIndex,
        string expectedWeekType)
    {
        var date = new DateOnly(year, month, day);

        await Assert.That(ScheduleMath.ResolveDayIndex(date)).IsEqualTo(expectedDayIndex);
        await Assert.That(ScheduleMath.WeekType(date)).IsEqualTo(expectedWeekType);
    }

    [Test]
    [Arguments(2026, 11, 19, 4, "lower")]
    [Arguments(2026, 11, 26, 11, "upper")]
    [Arguments(2026, 12, 3, 4, "lower")]
    public async Task AdjacentWeeksAlternateRotation(
        int year,
        int month,
        int day,
        int expectedDayIndex,
        string expectedWeekType)
    {
        var date = new DateOnly(year, month, day);

        await Assert.That(ScheduleMath.ResolveDayIndex(date)).IsEqualTo(expectedDayIndex);
        await Assert.That(ScheduleMath.WeekType(date)).IsEqualTo(expectedWeekType);
    }

    [Test]
    public async Task WeekdayIsIndependentOfRotation()
    {
        var lowerThursday = new DateOnly(2026, 11, 19);
        var upperThursday = new DateOnly(2026, 11, 26);

        await Assert.That(ScheduleMath.IsoWeekday(lowerThursday)).IsEqualTo(4);
        await Assert.That(ScheduleMath.IsoWeekday(upperThursday)).IsEqualTo(4);
        await Assert.That(ScheduleMath.ResolveDayIndex(upperThursday) -
                          ScheduleMath.ResolveDayIndex(lowerThursday)).IsEqualTo(7);
    }
}

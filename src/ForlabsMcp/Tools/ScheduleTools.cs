using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace ForlabsMcp.Tools;

[McpServerToolType]
public sealed class ScheduleTools(ForlabsApi api, ForlabsContext ctx)
{
    private static readonly TimeZoneInfo Tz = ResolveIrkutskTz();

    private static TimeZoneInfo ResolveIrkutskTz()
    {
        foreach (var id in new[] { "Asia/Irkutsk", "Russian Standard Time", "North Asia East Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.Utc;
    }

    private static DateOnly TodayLocal() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Tz).DateTime);

    [McpServerTool(Name = "forlabs_get_schedule_for_date"),
     Description("Returns the class schedule for one specific calendar date (defaults to today), resolved " +
                  "from Forlabs' two-week rotating grid. Use this for 'what do I have today/tomorrow' briefings.")]
    public async Task<string> GetScheduleForDate(
        [Description("Date as YYYY-MM-DD. Omit for today.")] string? date,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var target = date is null ? TodayLocal() : DateOnly.Parse(date);

        var (grid, schedule) = await FetchGridAndSchedule(streamId, ct);
        var upperWeek = grid?["grid"]?["upperweek"]?.GetValue<int>() ?? 1;
        var positions = grid?["grid"]?["positions"]?.AsArray().ToList() ?? [];
        var today = TodayLocal();

        var dayIndex = ScheduleMath.ResolveDayIndex(target, today, upperWeek);
        var lessons = JsonUtil.ArrayOf(schedule, "entries")
            .Where(e => e.Int("day") == dayIndex)
            .OrderBy(e => e.Int("position"))
            .Select(e => ShapeLesson(e, positions))
            .ToList();

        return JsonUtil.Pretty(new
        {
            date = target.ToString("yyyy-MM-dd"),
            weekday = ScheduleMath.WeekdayRu[ScheduleMath.IsoWeekday(target)],
            stream_id = streamId,
            lesson_count = lessons.Count,
            lessons,
        });
    }

    [McpServerTool(Name = "forlabs_get_schedule_for_week"),
     Description("Returns the class schedule for a 7-day span starting at the given date (defaults to the " +
                  "Monday of the current week). Use this for weekly planning briefings.")]
    public async Task<string> GetScheduleForWeek(
        [Description("Any date within the target week, as YYYY-MM-DD. Omit for the current week.")] string? date,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var anchor = date is null ? TodayLocal() : DateOnly.Parse(date);
        var monday = ScheduleMath.MondayOf(anchor);

        var (grid, schedule) = await FetchGridAndSchedule(streamId, ct);
        var upperWeek = grid?["grid"]?["upperweek"]?.GetValue<int>() ?? 1;
        var positions = grid?["grid"]?["positions"]?.AsArray().ToList() ?? [];
        var today = TodayLocal();
        var entries = JsonUtil.ArrayOf(schedule, "entries").ToList();

        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var d = monday.AddDays(offset);
            var dayIndex = ScheduleMath.ResolveDayIndex(d, today, upperWeek);
            var lessons = entries
                .Where(e => e.Int("day") == dayIndex)
                .OrderBy(e => e.Int("position"))
                .Select(e => ShapeLesson(e, positions))
                .ToList();
            return new
            {
                date = d.ToString("yyyy-MM-dd"),
                weekday = ScheduleMath.WeekdayRu[offset + 1],
                lessons,
            };
        }).ToList();

        return JsonUtil.Pretty(new { week_of = monday.ToString("yyyy-MM-dd"), stream_id = streamId, days });
    }

    [McpServerTool(Name = "forlabs_get_raw_two_week_schedule"),
     Description("Returns the full raw two-week rotating schedule (all entries tagged with Forlabs' internal " +
                  "day index 1-14, where 1-7 and 8-14 are the two alternating week variants) plus the grid " +
                  "metadata (lesson time slots, which half is currently the 'upper' week). Prefer " +
                  "forlabs_get_schedule_for_date / forlabs_get_schedule_for_week for normal use; use this only " +
                  "when you need the complete unresolved rotation.")]
    public async Task<string> GetRawTwoWeekSchedule(
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var (grid, schedule) = await FetchGridAndSchedule(streamId, ct);
        return JsonUtil.Pretty(new { grid, schedule });
    }

    private async Task<(JsonNode? grid, JsonNode? schedule)> FetchGridAndSchedule(int streamId, CancellationToken ct)
    {
        var gridTask = api.GetScheduleGridAsync(ct);
        var scheduleTask = api.GetScheduleAsync(streamId, ct);
        await Task.WhenAll(gridTask, scheduleTask);
        return (gridTask.Result, scheduleTask.Result);
    }

    private static object ShapeLesson(JsonObject e, List<JsonNode?> positions)
    {
        var pos = e.Int("position");
        JsonNode? slot = pos is int p && p >= 1 && p <= positions.Count ? positions[p - 1] : null;
        return new
        {
            position = pos,
            start = slot?["start"]?.ToString(),
            end = slot?["end"]?.ToString(),
            subject = e.Str("study_name"),
            study_id = e.Int("study_id"),
            lecturer = e.Str("lecturer_name"),
            room = e.Str("room_name"),
            type = e.Int("type"),
            subgroup = e.Str("subgroup"),
        };
    }
}

using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ForlabsMcp.Tools;

[McpServerToolType]
public sealed class ProgressTools(ForlabsApi api, ForlabsContext ctx)
{
    [McpServerTool(Name = "forlabs_get_scores_summary"),
     Description("Returns the credits/grade summary for every subject in the group (the 'зачётка' overview): " +
                  "accumulated credits (rating points) and grade per study_id.")]
    public async Task<string> GetScoresSummary(
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var studies = await ctx.ListStudiesAsync(streamId, ct);
        var namesById = studies
            .Where(s => s.Int("id") is not null)
            .ToDictionary(s => s.Int("id")!.Value, s => s.Str("verbose_name"));

        var resp = await api.GetScoresAsync(streamId, ct);
        var scores = resp?["scores"]?.AsObject();
        var shaped = scores?.Select(kv => new
        {
            study_id = int.Parse(kv.Key),
            subject = namesById.GetValueOrDefault(int.Parse(kv.Key)),
            credits = kv.Value?["credits"]?.ToString(),
            status = kv.Value?["status"]?.ToString(),
            grade = kv.Value?["grade"]?.ToString(),
        });

        return JsonUtil.Pretty(new { stream_id = streamId, scores = shaped });
    }

    [McpServerTool(Name = "forlabs_get_scoring_log"),
     Description("Returns the detailed point-by-point assessment log for a subject (each graded item: date, " +
                  "points awarded, grading teacher, and the cause/reason text).")]
    public async Task<string> GetScoringLog(
        [Description("Subject name (substring match) or numeric study_id.")] string subject,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var (studyId, subjectName) = await ctx.ResolveStudyAsync(streamId, subject, ct);
        var resp = await api.GetScoringAsync(streamId, studyId, ct);
        return JsonUtil.Pretty(new { stream_id = streamId, study_id = studyId, subject = subjectName, result = resp });
    }

    [McpServerTool(Name = "forlabs_get_attendance"),
     Description("Returns the attendance log for a subject: per-lesson date, cost (weight), and whether the " +
                  "student was marked present.")]
    public async Task<string> GetAttendance(
        [Description("Subject name (substring match) or numeric study_id.")] string subject,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var (studyId, subjectName) = await ctx.ResolveStudyAsync(streamId, subject, ct);
        var resp = await api.GetAttendanceAsync(streamId, studyId, ct);
        return JsonUtil.Pretty(new { stream_id = streamId, study_id = studyId, subject = subjectName, result = resp });
    }

    [McpServerTool(Name = "forlabs_get_recent_activity"),
     Description("Scans every subject in the student's *current* semester and returns grading events " +
                  "(assessment log entries — points awarded, by whom, for what) from the last N days — the " +
                  "complement to forlabs_get_upcoming_homework: 'what did I get graded on recently' rather " +
                  "than 'what's still due'. Useful for a 'what did I do today/this week' recap.")]
    public async Task<string> GetRecentActivity(
        [Description("How many days back to look. Default 7.")] int? days_back,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var earliest = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(days_back ?? 7));
        var studies = await ctx.ListCurrentStudiesAsync(streamId, ct);

        var perSubject = await Task.WhenAll(studies.Select(async s =>
        {
            var studyId = s.Int("id");
            if (studyId is null) return [];
            var resp = await api.GetScoringAsync(streamId, studyId.Value, ct);
            return JsonUtil.ArrayOf(resp, "assessments")
                .Where(a => a.Str("date") is not null && DateOnly.Parse(a.Str("date")!) >= earliest)
                .Select(a => new
                {
                    subject = s.Str("verbose_name"),
                    study_id = studyId.Value,
                    date = a.Str("date"),
                    credits = a["credits"]?.ToString(),
                    lecturer = a.Str("lecturer_name"),
                    cause = a.Str("cause"),
                })
                .ToArray();
        }));

        var activity = perSubject.SelectMany(x => x)
            .OrderByDescending(x => x.date)
            .ToList();

        return JsonUtil.Pretty(new
        {
            stream_id = streamId,
            since = earliest.ToString("yyyy-MM-dd"),
            entry_count = activity.Count,
            activity,
        });
    }

    [McpServerTool(Name = "forlabs_get_exams"),
     Description("Returns exam/test info for a subject: title, time limits, and — if already taken — the " +
                  "passing result (grade, points, percentage, credits earned).")]
    public async Task<string> GetExams(
        [Description("Subject name (substring match) or numeric study_id.")] string subject,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var (studyId, subjectName) = await ctx.ResolveStudyAsync(streamId, subject, ct);
        var resp = await api.GetExamsAsync(streamId, studyId, ct);
        return JsonUtil.Pretty(new { stream_id = streamId, study_id = studyId, subject = subjectName, result = resp });
    }
}

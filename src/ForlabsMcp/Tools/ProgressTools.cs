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

using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace ForlabsMcp.Tools;

[McpServerToolType]
public sealed class HomeworkTools(ForlabsApi api, ForlabsContext ctx)
{
    [McpServerTool(Name = "forlabs_get_homework"),
     Description("Lists homework/tasks for one subject: id, title, content (HTML), attached files, max points " +
                  "(pivot_cost), deadline (pivot_end_at) and status. Call forlabs_list_subjects first if you " +
                  "don't know the subject's study_id.")]
    public async Task<string> GetHomework(
        [Description("Subject name (substring match, e.g. 'Философия') or numeric study_id.")] string subject,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        [Description("Include tasks that already look graded/completed (status 3). Default false.")] bool? include_completed,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var (studyId, subjectName) = await ctx.ResolveStudyAsync(streamId, subject, ct);
        var resp = await api.GetTasksAsync(streamId, studyId, ct);

        var tasks = JsonUtil.ArrayOf(resp, "tasks")
            .Where(t => include_completed == true || t.Int("pivot_status") != 3)
            .Select(t => ShapeTask(t))
            .ToList();

        return JsonUtil.Pretty(new
        {
            stream_id = streamId,
            study_id = studyId,
            subject = subjectName,
            task_count = tasks.Count,
            tasks,
        });
    }

    [McpServerTool(Name = "forlabs_get_homework_details"),
     Description("Fetches the full detail of a single homework/task: complete HTML content, attached files " +
                  "(with direct download URLs), max points, deadline, and the linked 'assignment' (submission " +
                  "record id + status) needed by forlabs_get_task_chat. Use forlabs_download_task_file " +
                  "afterwards to fetch any attachments so you can actually read/use them.")]
    public async Task<string> GetHomeworkDetails(
        [Description("Subject name (substring match) or numeric study_id.")] string subject,
        [Description("The task id, as returned by forlabs_get_homework.")] int task_id,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var (studyId, subjectName) = await ctx.ResolveStudyAsync(streamId, subject, ct);
        var resp = await api.GetTaskAsync(streamId, studyId, task_id, ct);

        var task = resp?["task"] as JsonObject;
        var assignment = resp?["assignment"] as JsonObject;

        return JsonUtil.Pretty(new
        {
            stream_id = streamId,
            study_id = studyId,
            subject = subjectName,
            task = task is null ? null : ShapeTask(task, includeContent: true),
            assignment = assignment is null ? null : new
            {
                assignment_id = assignment.Int("id"),
                status = assignment.Int("status"),
                last_replied_at = assignment.Str("last_replied_at"),
                responses_count = assignment.Int("responses_count"),
                assessment_credits = assignment["assessment_credits"]?.ToString(),
                assessment_date = assignment.Str("assessment_date"),
            },
        });
    }

    [McpServerTool(Name = "forlabs_get_upcoming_homework"),
     Description("Scans every subject in the student's *current* semester and returns homework due within the " +
                  "next N days that doesn't look completed yet — designed for a morning briefing / 'what's " +
                  "due soon' digest. Only scans current-semester subjects (see forlabs_list_subjects' 'status' " +
                  "field), so it won't resurface homework from finished semesters even if Forlabs never marked " +
                  "it graded; within the current semester, already-overdue-but-still-pending tasks are included " +
                  "with no cutoff, since those are exactly the ones worth surfacing. Tasks without a deadline " +
                  "are omitted (they're not time-boxed). Pass include_completed=true for a 'what have I already " +
                  "turned in' view instead of (or in addition to) 'what's still pending'.")]
    public async Task<string> GetUpcomingHomework(
        [Description("How many days ahead to look. Default 7.")] int? days_ahead,
        [Description("Also include tasks that already look graded/completed (status 3). Default false.")] bool? include_completed,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var horizon = DateTimeOffset.UtcNow.AddDays(days_ahead ?? 7);
        var studies = await ctx.ListCurrentStudiesAsync(streamId, ct);

        var perSubject = await Task.WhenAll(studies.Select(async s =>
        {
            var studyId = s.Int("id");
            if (studyId is null) return [];
            var resp = await api.GetTasksAsync(streamId, studyId.Value, ct);
            return JsonUtil.ArrayOf(resp, "tasks")
                .Where(t => (include_completed == true || t.Int("pivot_status") != 3) && t["pivot_end_at"] is not null)
                .Select(t =>
                {
                    var deadline = DateTimeOffset.Parse(t.Str("pivot_end_at")!);
                    return (deadline, subject: s.Str("verbose_name"), studyId: studyId.Value, task: t);
                })
                .Where(x => x.deadline <= horizon)
                .ToArray();
        }));

        var due = perSubject.SelectMany(x => x)
            .OrderBy(x => x.deadline)
            .Select(x => new
            {
                subject = x.subject,
                study_id = x.studyId,
                deadline = x.deadline.ToString("yyyy-MM-dd HH:mm"),
                days_left = Math.Round((x.deadline - DateTimeOffset.UtcNow).TotalDays, 1),
                overdue = x.deadline < DateTimeOffset.UtcNow,
                task = ShapeTask(x.task, includeContent: false),
            })
            .ToList();

        return JsonUtil.Pretty(new
        {
            stream_id = streamId,
            horizon_days = days_ahead ?? 7,
            due_count = due.Count,
            due,
        });
    }

    [McpServerTool(Name = "forlabs_get_course_materials"),
     Description("Lists the course chapters/materials structure for a subject (titles, whether they have " +
                  "content, block counts). Note: this captures only the chapter index — the Forlabs API for " +
                  "fetching a chapter's actual content blocks was not observed in the recorded traffic this " +
                  "server was built from, so block contents are not retrievable here.")]
    public async Task<string> GetCourseMaterials(
        [Description("Subject name (substring match) or numeric study_id.")] string subject,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var (studyId, subjectName) = await ctx.ResolveStudyAsync(streamId, subject, ct);
        var resp = await api.GetChaptersAsync(streamId, studyId, ct);
        return JsonUtil.Pretty(new { stream_id = streamId, study_id = studyId, subject = subjectName, result = resp });
    }

    private static object ShapeTask(JsonObject t, bool includeContent = false) => new
    {
        id = t.Int("id"),
        name = t.Str("name"),
        content = includeContent ? t.Str("content") : Truncate(t.Str("content"), 200),
        max_points = t.Int("pivot_cost"),
        status = t.Int("pivot_status"),
        status_hint = StatusHint(t.Int("pivot_status")),
        deadline = t.Str("pivot_end_at"),
        opens_at = t.Str("pivot_start_at"),
        description = t.Str("pivot_description"),
        chapter = t.Str("chapter_title"),
        files = t["files"]?.AsArray().Select(f => new
        {
            filename = f?["filename"]?.ToString(),
            url = f?["url"]?.ToString(),
            size = f?["human_size"]?.ToString(),
            mime_type = f?["mime_type"]?.ToString(),
        }),
    };

    private static string StatusHint(int? status) => status switch
    {
        1 => "not started / not submitted (best-effort guess — Forlabs doesn't document this code)",
        2 => "submitted, likely pending review (best-effort guess)",
        3 => "graded / completed (best-effort guess)",
        _ => "unknown",
    };

    private static string? Truncate(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max] + "…";
}

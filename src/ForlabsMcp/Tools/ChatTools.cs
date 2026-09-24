using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace ForlabsMcp.Tools;

[McpServerToolType]
public sealed class ChatTools(ForlabsApi api, ForlabsContext ctx)
{
    [McpServerTool(Name = "forlabs_get_announcements"),
     Description("Lists announcements/posts for a subject (e.g. exam procedure notices, schedule changes).")]
    public async Task<string> GetAnnouncements(
        [Description("Subject name (substring match) or numeric study_id.")] string subject,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var (studyId, subjectName) = await ctx.ResolveStudyAsync(streamId, subject, ct);
        var resp = await api.GetPostsAsync(streamId, studyId, ct);

        var posts = JsonUtil.ArrayOf(resp, "posts").Select(p => new
        {
            id = p.Int("id"),
            title = p.Str("title"),
            content = p.Str("content"),
            created_at = p.Str("created_at"),
        });

        return JsonUtil.Pretty(new { stream_id = streamId, study_id = studyId, subject = subjectName, posts });
    }

    [McpServerTool(Name = "forlabs_get_task_chat"),
     Description("Reads the per-assignment chat thread with the teacher for one homework task (feedback, " +
                  "clarifications, grading comments). Call forlabs_get_homework_details first to obtain the " +
                  "assignment_id. Note: sending new messages/submissions was not observed in the recorded " +
                  "traffic this server was built from, so this tool is read-only.")]
    public async Task<string> GetTaskChat(
        [Description("The study_id the task belongs to.")] int study_id,
        [Description("The task id.")] int task_id,
        [Description("The assignment id, from forlabs_get_homework_details' 'assignment.assignment_id' field.")] int assignment_id,
        CancellationToken ct)
    {
        var resp = await api.GetCommentsAsync(study_id, task_id, assignment_id, ct);
        var comments = JsonUtil.ArrayOf(resp, "comments").Select(c => new
        {
            id = c.Int("id"),
            author = (c["user"] as JsonObject).Str("name"),
            text = c.Str("message"),
            created_at = c.Str("created_at"),
            files = c["attachments"]?.AsArray().Select(f => new
            {
                filename = f?["filename"]?.ToString(),
                url = f?["url"]?.ToString(),
            }),
        });

        return JsonUtil.Pretty(new { study_id, task_id, assignment_id, comments });
    }
}

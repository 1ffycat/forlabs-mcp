using System.ComponentModel;
using ModelContextProtocol;
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
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id = null,
        CancellationToken ct = default)
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
                  "clarifications, grading comments), oldest first. Each comment has is_own=true when written " +
                  "by the logged-in student (a comment with text null and only files is the student's own " +
                  "upload). assignment_id is resolved automatically from the task list when omitted. " +
                  "Sending new messages/submissions was not observed in the recorded traffic this server " +
                  "was built from, so this tool is read-only.")]
    public async Task<string> GetTaskChat(
        [Description("Subject name (substring match) or numeric study_id.")] string subject,
        [Description("The task id, as returned by forlabs_get_homework.")] int task_id,
        [Description("The assignment id (forlabs_get_homework_details' 'assignment.assignment_id'). Omit to resolve it automatically.")] int? assignment_id = null,
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id = null,
        CancellationToken ct = default)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var (studyId, subjectName) = await ctx.ResolveStudyAsync(streamId, subject, ct);

        var assignmentId = assignment_id;
        if (assignmentId is null)
        {
            var tasks = await api.GetTasksAsync(streamId, studyId, ct);
            if (!JsonUtil.ArrayOf(tasks, "tasks").Any(t => t.Int("id") == task_id))
                throw new McpException($"Task {task_id} was not found in subject \"{subjectName}\" (study_id={studyId}). " +
                                       "Get task ids from forlabs_get_homework.");
            assignmentId = HomeworkTools.AssignmentsByTaskId(tasks).GetValueOrDefault(task_id)?.Int("id")
                ?? throw new McpException($"Task {task_id} has no assignment yet (nothing submitted), so there is no chat.");
        }

        var ownId = await ctx.ResolveOwnUserIdAsync(ct);
        var resp = await api.GetCommentsAsync(studyId, task_id, assignmentId.Value, ct);
        var comments = JsonUtil.ArrayOf(resp, "comments")
            .OrderBy(c => c.Str("created_at"), StringComparer.Ordinal)
            .Select(c =>
            {
                var user = c["user"] as JsonObject;
                var authorId = user.Int("id") ?? c.Int("user_id");
                return new
                {
                    id = c.Int("id"),
                    author = user.Str("name"),
                    author_id = authorId,
                    is_own = authorId is not null && authorId == ownId,
                    text = c.Str("message"),
                    created_at = c.Str("created_at"),
                    files = c["attachments"]?.AsArray().Select(f => new
                    {
                        filename = f?["filename"]?.ToString(),
                        url = f?["url"]?.ToString(),
                    }),
                };
            })
            .ToList();

        return JsonUtil.Pretty(new
        {
            stream_id = streamId,
            study_id = studyId,
            subject = subjectName,
            task_id,
            assignment_id = assignmentId,
            comments,
        });
    }
}

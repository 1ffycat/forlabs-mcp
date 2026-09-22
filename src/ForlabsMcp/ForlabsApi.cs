using System.Text.Json.Nodes;

namespace ForlabsMcp;

/// <summary>
/// Typed wrappers around the Forlabs "lm-vendor/repositories/*" JSON-RPC-ish endpoints.
/// Every method just posts/gets the same request the Angular frontend makes and hands
/// back the raw JsonNode — tools layer decides how much of it to expose to the model.
/// </summary>
public sealed class ForlabsApi(ForlabsClient client)
{
    private const string Repo = "lm-vendor/repositories/";

    public Task<JsonNode?> GetProfileAsync(CancellationToken ct) =>
        client.GetJsonAsync("app/profile/user", ct);

    public Task<JsonNode?> GetScheduleGridAsync(CancellationToken ct) =>
        client.PostJsonAsync(Repo + "sched/get_grid", null, ct);

    public Task<JsonNode?> GetScheduleAsync(int streamId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "sched/get_schedule", new { stream_id = streamId }, ct);

    /// <summary>The student's own academic group(s) ("streams" in Forlabs terms).</summary>
    public Task<JsonNode?> GetMyStreamsAsync(CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_streams", null, ct);

    public Task<JsonNode?> GetStudiesAsync(int streamId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_studies", new { stream_id = streamId }, ct);

    public Task<JsonNode?> GetScoresAsync(int streamId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_scores", new { stream_id = streamId }, ct);

    public Task<JsonNode?> GetTasksAsync(int streamId, int studyId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_tasks",
            new { stream_id = streamId.ToString(), study_id = studyId.ToString() }, ct);

    public Task<JsonNode?> GetTaskAsync(int streamId, int studyId, int taskId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_task",
            new { stream_id = streamId.ToString(), study_id = studyId.ToString(), task_id = taskId.ToString() }, ct);

    public Task<JsonNode?> GetChaptersAsync(int streamId, int studyId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_chapters",
            new { stream_id = streamId.ToString(), study_id = studyId.ToString() }, ct);

    public Task<JsonNode?> GetExamsAsync(int streamId, int studyId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_exams",
            new { stream_id = streamId.ToString(), study_id = studyId.ToString() }, ct);

    public Task<JsonNode?> GetAttendanceAsync(int streamId, int studyId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_attendance",
            new { stream_id = streamId.ToString(), study_id = studyId.ToString() }, ct);

    public Task<JsonNode?> GetScoringAsync(int streamId, int studyId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_scoring",
            new { stream_id = streamId.ToString(), study_id = studyId.ToString() }, ct);

    public Task<JsonNode?> GetPostsAsync(int streamId, int studyId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "learning/get_posts",
            new { stream_id = streamId.ToString(), study_id = studyId.ToString() }, ct);

    public Task<JsonNode?> GetCommentsAsync(int studyId, int taskId, int assignmentId, CancellationToken ct) =>
        client.PostJsonAsync(Repo + "assignments/get_comments",
            new { study_id = studyId.ToString(), task_id = taskId, assignment_id = assignmentId }, ct);
}

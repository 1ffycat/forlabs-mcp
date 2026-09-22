using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ForlabsMcp.Tools;

[McpServerToolType]
public sealed class ProfileTools(ForlabsApi api, ForlabsContext ctx)
{
    [McpServerTool(Name = "forlabs_whoami"),
     Description("Returns the logged-in Forlabs student's profile: name, email, and their academic group (stream).")]
    public async Task<string> WhoAmI(CancellationToken ct)
    {
        var profile = await api.GetProfileAsync(ct);
        return JsonUtil.Pretty(profile);
    }

    [McpServerTool(Name = "forlabs_list_subjects"),
     Description("Lists all subjects (courses) for the student's current semester, with their study_id " +
                  "(needed by other tools), lecturers, and credit/ЗЕТ info. Call this first to discover " +
                  "which subject names/IDs are available before asking about homework, posts, exams, etc.")]
    public async Task<string> ListSubjects(
        [Description("Academic group (stream) id. Omit to use the logged-in student's own group.")] int? stream_id,
        CancellationToken ct)
    {
        var streamId = stream_id ?? await ctx.ResolveOwnStreamIdAsync(ct);
        var studies = await ctx.ListStudiesAsync(streamId, ct);
        var shaped = studies.Select(s => new
        {
            study_id = s.Int("id"),
            name = s.Str("verbose_name"),
            short_name = s.Str("short_name"),
            year = s.Str("year_name"),
            semester = s.Int("curr_sem"),
            status = s.Int("status"),
            lecturers = s["lecturers"]?.AsArray().Select(l => l?["verbose_name"]?.ToString()),
        });
        return JsonUtil.Pretty(new { stream_id = streamId, subjects = shaped });
    }
}

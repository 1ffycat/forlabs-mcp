using System.Text.Json.Nodes;

namespace ForlabsMcp;

/// <summary>
/// Session-scoped conveniences layered on top of <see cref="ForlabsApi"/>:
/// resolving "my own group" and letting tools accept a subject by name
/// instead of forcing the caller to already know its numeric study_id.
/// </summary>
public sealed class ForlabsContext(ForlabsApi api)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int? _ownStreamId;
    private readonly Dictionary<int, (DateTime fetchedAt, List<JsonObject> studies)> _studiesCache = new();

    public async Task<int> ResolveOwnStreamIdAsync(CancellationToken ct)
    {
        if (_ownStreamId is int cached) return cached;
        await _lock.WaitAsync(ct);
        try
        {
            if (_ownStreamId is int cached2) return cached2;

            var profile = await api.GetProfileAsync(ct);
            var fromProfile = profile?["students"]?.AsArray()?
                .Select(s => (int?)s?["stream_id"])
                .FirstOrDefault(v => v is not null);
            if (fromProfile is int id)
            {
                _ownStreamId = id;
                return id;
            }

            var streams = await api.GetMyStreamsAsync(ct);
            var first = streams?["streams"]?.AsArray()?.FirstOrDefault();
            if (first?["id"] is JsonNode idNode && idNode.GetValue<int>() is var sid)
            {
                _ownStreamId = sid;
                return sid;
            }

            throw new InvalidOperationException(
                "Could not determine the student's own stream_id from /app/profile/user or get_streams. " +
                "Pass stream_id explicitly.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<JsonObject>> ListStudiesAsync(int streamId, CancellationToken ct)
    {
        if (_studiesCache.TryGetValue(streamId, out var entry) &&
            DateTime.UtcNow - entry.fetchedAt < TimeSpan.FromMinutes(10))
        {
            return entry.studies;
        }

        var resp = await api.GetStudiesAsync(streamId, ct);
        var list = resp?["studies"]?.AsArray()
            .Select(n => n as JsonObject)
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList() ?? [];

        _studiesCache[streamId] = (DateTime.UtcNow, list);
        return list;
    }

    /// <summary>
    /// Studies (subjects) belonging to the current/active semester only — Forlabs keeps every
    /// past semester's subjects in get_studies forever, distinguished only by a "status" field
    /// (2 = current, 3 = finished; not documented anywhere, inferred from a live account where
    /// every 2026/2027-semester-7 subject was 2 and every older one was 3). Used to keep
    /// cross-subject digests (upcoming homework, recent grades) from resurfacing years-old,
    /// long-dead-semester tasks that never got a terminal status.
    /// </summary>
    public async Task<List<JsonObject>> ListCurrentStudiesAsync(int streamId, CancellationToken ct)
    {
        var all = await ListStudiesAsync(streamId, ct);
        return all.Where(s => s.Int("status") == 2).ToList();
    }

    /// <summary>
    /// Accepts either a numeric study_id (as a string) or a subject name / partial
    /// name (case-insensitive, substring match) and resolves it to a study_id.
    /// Throws with the list of candidate subjects when it can't find an unambiguous match.
    /// </summary>
    public async Task<(int studyId, string subjectName)> ResolveStudyAsync(
        int streamId, string subject, CancellationToken ct)
    {
        if (int.TryParse(subject, out var direct))
        {
            var studies = await ListStudiesAsync(streamId, ct);
            var match = studies.FirstOrDefault(s => (int?)s["id"] == direct);
            var name = match?["verbose_name"]?.GetValue<string>() ?? match?["short_name"]?.GetValue<string>() ?? subject;
            return (direct, name);
        }

        var all = await ListStudiesAsync(streamId, ct);
        var needle = subject.Trim();

        var exact = all.Where(s =>
            string.Equals(s["verbose_name"]?.GetValue<string>(), needle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count == 1)
        {
            return ((int)exact[0]["id"]!, exact[0]["verbose_name"]!.GetValue<string>());
        }

        var contains = all.Where(s =>
            (s["verbose_name"]?.GetValue<string>() ?? "")
                .Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (contains.Count == 1)
        {
            return ((int)contains[0]["id"]!, contains[0]["verbose_name"]!.GetValue<string>());
        }

        if (contains.Count == 0)
        {
            var names = string.Join(", ", all.Select(s => s["verbose_name"]?.GetValue<string>()));
            throw new InvalidOperationException(
                $"No subject matching \"{subject}\" was found for stream {streamId}. Available subjects: {names}");
        }

        var options = string.Join(", ", contains.Select(s => $"{s["verbose_name"]?.GetValue<string>()} (id={s["id"]})"));
        throw new InvalidOperationException(
            $"\"{subject}\" matches more than one subject: {options}. Pass the numeric id or a more specific name.");
    }
}

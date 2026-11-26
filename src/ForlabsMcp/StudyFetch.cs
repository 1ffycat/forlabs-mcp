using ModelContextProtocol;
using System.Text.Json.Nodes;

namespace ForlabsMcp;

internal sealed record StudyFetchResult<T>(int? StudyId, string? Subject, T? Value, string? Error)
    where T : class;

/// <summary>Fetches independent per-study data concurrently without discarding successful results.</summary>
internal static class StudyFetch
{
    public static async Task<IReadOnlyList<StudyFetchResult<T>>> AllAsync<T>(
        IEnumerable<JsonObject> studies,
        Func<int, CancellationToken, Task<T>> fetch,
        CancellationToken cancellationToken)
        where T : class
    {
        return await Task.WhenAll(studies.Select(async study =>
        {
            var studyId = study.Int("id");
            var subject = study.Str("verbose_name");
            if (studyId is null)
                return new StudyFetchResult<T>(null, subject, null, "The study entry has no id.");

            try
            {
                var value = await fetch(studyId.Value, cancellationToken);
                return new StudyFetchResult<T>(studyId, subject, value, null);
            }
            catch (McpException ex) when (!cancellationToken.IsCancellationRequested)
            {
                return new StudyFetchResult<T>(studyId, subject, null, ex.Message);
            }
        }));
    }

    public static string[] Warnings<T>(IEnumerable<StudyFetchResult<T>> results)
        where T : class => results
            .Where(result => result.Error is not null)
            .Select(result => $"Could not read {result.Subject ?? "an unnamed subject"} " +
                              $"(study_id={result.StudyId?.ToString() ?? "unknown"}): {result.Error}")
            .ToArray();
}

using ModelContextProtocol;
using System.Text.Json.Nodes;

namespace ForlabsMcp.Tests;

public sealed class StudyFetchTests
{
    [Test]
    public async Task ReturnsSuccessfulStudiesAlongsideFailures()
    {
        var studies = new[]
        {
            Study(11, "Psychology"),
            Study(12, "Philosophy"),
        };

        var results = await StudyFetch.AllAsync<JsonArray>(studies, (studyId, _) =>
            studyId == 11
                ? Task.FromResult(new JsonArray(JsonValue.Create("ok")))
                : Task.FromException<JsonArray>(new McpException("upstream unavailable")),
            CancellationToken.None);

        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results.Single(result => result.StudyId == 11).Value).IsNotNull();
        await Assert.That(results.Single(result => result.StudyId == 12).Error).IsEqualTo("upstream unavailable");
        await Assert.That(StudyFetch.Warnings(results).Length).IsEqualTo(1);
        await Assert.That(StudyFetch.Warnings(results)[0]).Contains("Philosophy");
    }

    [Test]
    public async Task MissingStudyIdIsReportedAsFailure()
    {
        var results = await StudyFetch.AllAsync<JsonArray>(
            [new JsonObject { ["verbose_name"] = "Missing id" }],
            (_, _) => throw new InvalidOperationException("Fetch must not run."),
            CancellationToken.None);

        await Assert.That(results.Single().Error).IsNotNull();
        await Assert.That(StudyFetch.Warnings(results).Length).IsEqualTo(1);
    }

    [Test]
    public async Task CancellationIsNotConvertedIntoPartialSuccess()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        var cancelled = false;
        try
        {
            await StudyFetch.AllAsync<JsonArray>(
                [Study(11, "Psychology")],
                (_, token) => Task.FromCanceled<JsonArray>(token),
                source.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        await Assert.That(cancelled).IsTrue();
    }

    private static JsonObject Study(int id, string name) =>
        new() { ["id"] = id, ["verbose_name"] = name };
}

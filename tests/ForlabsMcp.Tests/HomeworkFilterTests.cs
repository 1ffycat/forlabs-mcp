using System.Text.Json.Nodes;
using ForlabsMcp.Tools;

namespace ForlabsMcp.Tests;

public sealed class HomeworkFilterTests
{
    [Test]
    public async Task SearchMatchesTitleChapterAndDescriptionIgnoringCase()
    {
        var filters = HomeworkFilter.Create("  LECTURE ", null, null, null, null, 0);
        var title = Task("Lecture notes", null, null);
        var chapter = Task("Task", "Lecture 4", null);
        var description = Task("Task", null, "Read lecture notes");
        var unrelated = Task("Essay", "Chapter 2", "Write a response");

        await Assert.That(filters.Matches(title)).IsTrue();
        await Assert.That(filters.Matches(chapter)).IsTrue();
        await Assert.That(filters.Matches(description)).IsTrue();
        await Assert.That(filters.Matches(unrelated)).IsFalse();
    }

    [Test]
    public async Task DueFiltersUseInclusiveUtcCalendarDates()
    {
        var filters = HomeworkFilter.Create(
            null, true, "2026-11-26", "2026-11-27", null, 0);

        await Assert.That(filters.Matches(Task("Start", null, null, "2026-11-26T00:00:00Z"))).IsTrue();
        await Assert.That(filters.Matches(Task("End", null, null, "2026-11-27T23:59:00Z"))).IsTrue();
        await Assert.That(filters.Matches(Task("Before", null, null, "2026-11-25T23:59:00Z"))).IsFalse();
        await Assert.That(filters.Matches(Task("After", null, null, "2026-11-28T00:00:00Z"))).IsFalse();
        await Assert.That(filters.Matches(Task("No due date", null, null))).IsFalse();
    }

    [Test]
    public async Task HasDueFalseIncludesUndatedTasksOnly()
    {
        var filters = HomeworkFilter.Create(null, false, null, null, null, 0);

        await Assert.That(filters.Matches(Task("Undated", null, null))).IsTrue();
        await Assert.That(filters.Matches(Task("Dated", null, null, "2026-11-26T12:00:00Z"))).IsFalse();
        await Assert.That(filters.Matches(Task("Malformed date", null, null, "invalid"))).IsFalse();
    }

    [Test]
    public async Task InvalidDateRangeAndPagingArgumentsAreRejected()
    {
        var badRange = ThrowsArgumentException(() =>
            HomeworkFilter.Create(null, null, "2026-11-27", "2026-11-26", null, 0));
        var badDate = ThrowsArgumentException(() =>
            HomeworkFilter.Create(null, null, "26-11-2026", null, null, 0));
        var badLimit = ThrowsArgumentException(() =>
            HomeworkFilter.Create(null, null, null, null, 0, 0));
        var badOffset = ThrowsArgumentException(() =>
            HomeworkFilter.Create(null, null, null, null, null, -1));

        await Assert.That(badRange).IsTrue();
        await Assert.That(badDate).IsTrue();
        await Assert.That(badLimit).IsTrue();
        await Assert.That(badOffset).IsTrue();
    }

    [Test]
    public async Task PagingReportsFilteredTotalAndWhetherMoreItemsRemain()
    {
        var filter = HomeworkFilter.Create(null, null, null, null, 2, 1);
        var page = filter.Page(new[] { "a", "b", "c", "d" });
        var finalPage = HomeworkFilter.Create(null, null, null, null, 2, 3)
            .Page(new[] { "a", "b", "c", "d" });

        await Assert.That(page.Items).IsEquivalentTo(new[] { "b", "c" });
        await Assert.That(page.TotalCount).IsEqualTo(4);
        await Assert.That(page.HasMore).IsTrue();
        await Assert.That(finalPage.Items).IsEquivalentTo(new[] { "d" });
        await Assert.That(finalPage.HasMore).IsFalse();

        var beyondEnd = HomeworkFilter.Create(null, null, null, null, 2, int.MaxValue)
            .Page(new[] { "a", "b" });
        await Assert.That(beyondEnd.Items).IsEmpty();
        await Assert.That(beyondEnd.HasMore).IsFalse();
    }

    [Test]
    public async Task AssignmentCompletionUsesAssignmentStatusAndNotTaskStatus()
    {
        await Assert.That(HomeworkTools.IsCompleted(JsonNode.Parse("""{"status":3}""")!.AsObject())).IsTrue();
        await Assert.That(HomeworkTools.IsCompleted(JsonNode.Parse("""{"status":6}""")!.AsObject())).IsTrue();
        await Assert.That(HomeworkTools.IsCompleted(JsonNode.Parse("""{"status":1}""")!.AsObject())).IsFalse();
        await Assert.That(HomeworkTools.IsCompleted(JsonNode.Parse("""{"status":2}""")!.AsObject())).IsFalse();
        await Assert.That(HomeworkTools.IsCompleted(null)).IsFalse();
    }

    [Test]
    public async Task AssignmentsAreMatchedByTaskId()
    {
        var response = JsonNode.Parse("""{"assignments":[{"id":901,"task_id":17,"status":3}]}""");
        var assignments = HomeworkTools.AssignmentsByTaskId(response);

        await Assert.That(assignments.ContainsKey(17)).IsTrue();
        await Assert.That(assignments.ContainsKey(901)).IsFalse();
        await Assert.That(HomeworkTools.IsCompleted(assignments[17])).IsTrue();
    }

    private static JsonObject Task(string name, string? chapter, string? description, string? due = null) =>
        new()
        {
            ["name"] = name,
            ["chapter_title"] = chapter,
            ["pivot_description"] = description,
            ["pivot_end_at"] = due,
        };

    private static bool ThrowsArgumentException(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}

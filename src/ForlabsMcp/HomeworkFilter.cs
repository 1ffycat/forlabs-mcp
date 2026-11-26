using System.Globalization;
using System.Text.Json.Nodes;

namespace ForlabsMcp;

internal sealed record HomeworkFilter(
    string? Query,
    bool? HasDue,
    DateOnly? DueFrom,
    DateOnly? DueTo,
    int? Limit,
    int Offset)
{
    public HomeworkPage<T> Page<T>(IEnumerable<T> matching)
    {
        var rows = matching.ToList();
        var items = rows.Skip(Offset).Take(Limit ?? int.MaxValue).ToArray();
        return new HomeworkPage<T>(items, rows.Count, Limit, Offset, (long)Offset + items.Length < rows.Count);
    }

    public static HomeworkFilter Create(
        string? query,
        bool? hasDue,
        string? dueFrom,
        string? dueTo,
        int? limit,
        int offset)
    {
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be a positive integer.");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "offset must be a non-negative integer.");

        var from = ParseDate(dueFrom, nameof(dueFrom));
        var to = ParseDate(dueTo, nameof(dueTo));
        if (from is not null && to is not null && from > to)
            throw new ArgumentException("due_from must not be later than due_to.", nameof(dueFrom));

        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        return new HomeworkFilter(normalizedQuery, hasDue, from, to, limit, offset);
    }

    public bool Matches(JsonObject task)
    {
        var deadlineText = task.Str("pivot_end_at");
        var hasDeadline = !string.IsNullOrWhiteSpace(deadlineText);
        DateOnly? deadline = null;
        if (hasDeadline && DateTimeOffset.TryParse(
                deadlineText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            deadline = DateOnly.FromDateTime(parsed.UtcDateTime);

        if (HasDue == true && !hasDeadline || HasDue == false && hasDeadline)
            return false;
        if (DueFrom is not null && (deadline is null || deadline.Value < DueFrom.Value))
            return false;
        if (DueTo is not null && (deadline is null || deadline.Value > DueTo.Value))
            return false;

        if (Query is null)
            return true;

        return Contains(task.Str("name"), Query) ||
               Contains(task.Str("chapter_title"), Query) ||
               Contains(task.Str("pivot_description"), Query);
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private static DateOnly? ParseDate(string? value, string argument)
    {
        if (value is null)
            return null;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            return parsed;
        throw new ArgumentException($"{argument} must be an ISO date (YYYY-MM-DD).", argument);
    }
}

internal sealed record HomeworkPage<T>(IReadOnlyList<T> Items, int TotalCount, int? Limit, int Offset, bool HasMore);

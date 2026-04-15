namespace Workflow.Meetings.Domain;

/// <summary>
/// Generates a unique Meeting Number in the format "{seq}/{BE-year}" where BE-year = Gregorian + 543.
/// Implementation must be transactionally safe (UPDLOCK/HOLDLOCK) to avoid duplicates under concurrency.
/// </summary>
public interface IMeetingNoGenerator
{
    Task<string> NextAsync(DateTime now, CancellationToken ct);
}

namespace Shared.Messaging.Events;

/// <summary>
/// An INTERNAL staff member has been assigned to work an appraisal. Raised for both paths that put
/// bank staff on the job: the in-house appraisal (int-appraisal-execution) and the off-system
/// external key-in (int-offline-book-keyin), where an internal appraiser transcribes a book produced
/// by a company the bank engaged outside the system.
/// </summary>
public record InternalAssignedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public string AssigneeUserId { get; init; } = default!;
    public string? InternalAppraiserId { get; init; }
    public string AssignmentMethod { get; init; } = "RoundRobin";
    public string? InternalFollowupAssignmentMethod { get; init; }
    public string? CompletedBy { get; init; }
    public string? AppraisalNumber { get; init; }

    /// <summary>
    /// What the CASE counts as, which is not the same as who works it. "Internal" for the in-house
    /// path; "External" for an off-system engagement, where the assignee is internal but the
    /// appraisal was produced by an external company and must stay External for reporting, the
    /// AS400/LOS feed and fee resolution. Defaults to "Internal" so existing publishers are
    /// unaffected.
    /// </summary>
    public string AssignmentType { get; init; } = "Internal";
}

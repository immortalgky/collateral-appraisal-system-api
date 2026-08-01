using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record MeetingItemReleasedDomainEvent(
    Guid MeetingId,
    Guid AppraisalId,
    Guid WorkflowInstanceId,
    string ActivityId,
    string ReleasedBy,
    IReadOnlyList<MeetingApprover> Members) : IDomainEvent;

/// <summary>
/// A meeting member as the downstream approval sees them: the user who votes and the
/// meeting position that becomes their approval role. <paramref name="Role"/> is the
/// <see cref="Workflow.Domain.Committees.CommitteeMemberPosition"/> name, so committee
/// <c>RoleRequired</c> conditions keep matching once the roster drives the approval.
/// </summary>
public record MeetingApprover(string UserId, string Role);

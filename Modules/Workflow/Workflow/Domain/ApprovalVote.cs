namespace Workflow.Domain;

public class ApprovalVote : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }

    /// <summary>
    /// The workflow instance that produced this vote. NULL for votes imported from legacy
    /// completed appraisals, which have no <c>workflow.WorkflowInstances</c> row — the
    /// approval-history endpoint serves those from <c>appraisal.AppraisalReviews</c> instead.
    /// </summary>
    public Guid? WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public Guid ActivityExecutionId { get; private set; }
    public string Member { get; private set; } = default!;
    public string? MemberRole { get; private set; }
    public string Vote { get; private set; } = default!;
    public string? Comments { get; private set; }
    public DateTime VotedAt { get; private set; }

    private ApprovalVote() { }

    public static ApprovalVote Create(
        Guid appraisalId, Guid workflowInstanceId, string activityId, Guid activityExecutionId,
        string member, string? memberRole, string vote, string? comments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(member);
        ArgumentException.ThrowIfNullOrWhiteSpace(vote);

        return new ApprovalVote
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            ActivityExecutionId = activityExecutionId,
            Member = member,
            MemberRole = memberRole,
            Vote = vote,
            Comments = comments,
            VotedAt = DateTime.Now
        };
    }
}

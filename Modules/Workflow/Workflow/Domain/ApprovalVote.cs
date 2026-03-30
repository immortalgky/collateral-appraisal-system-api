namespace Workflow.Domain;

public class ApprovalVote : Entity<Guid>
{
    public Guid WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public Guid ActivityExecutionId { get; private set; }
    public string Member { get; private set; } = default!;
    public string? MemberRole { get; private set; }
    public string Vote { get; private set; } = default!;
    public string? Comments { get; private set; }
    public DateTime VotedAt { get; private set; }

    private ApprovalVote() { }

    public static ApprovalVote Create(
        Guid workflowInstanceId, string activityId, Guid activityExecutionId,
        string member, string? memberRole, string vote, string? comments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(member);
        ArgumentException.ThrowIfNullOrWhiteSpace(vote);

        return new ApprovalVote
        {
            Id = Guid.CreateVersion7(),
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            ActivityExecutionId = activityExecutionId,
            Member = member,
            MemberRole = memberRole,
            Vote = vote,
            Comments = comments,
            VotedAt = DateTime.UtcNow
        };
    }
}

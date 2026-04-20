using Shared.DDD;

namespace Workflow.Meetings.Domain;

public class MeetingItem : Entity<Guid>
{
    public Guid MeetingId { get; private set; }
    public Guid AppraisalId { get; private set; }
    public string? AppraisalNo { get; private set; }
    public decimal FacilityLimit { get; private set; }

    /// <summary>Only populated for Decision items (null for Acknowledgement items).</summary>
    public Guid? WorkflowInstanceId { get; private set; }

    /// <summary>Only populated for Decision items (null for Acknowledgement items).</summary>
    public string? ActivityId { get; private set; }

    public DateTime AddedAt { get; private set; }

    // ----- New fields (Phase 1) -----

    /// <summary>Whether this item is a workflow decision item or an acknowledgement item.</summary>
    public MeetingItemKind Kind { get; private set; }

    /// <summary>Appraisal type captured at cut-off (e.g. "New", "ReAppraisal").</summary>
    public string? AppraisalType { get; private set; }

    /// <summary>Only populated for Acknowledgement items (e.g. "Group1", "UrgentGroup2").</summary>
    public string? AcknowledgementGroup { get; private set; }

    /// <summary>Source <see cref="AppraisalAcknowledgementQueueItem"/> ID for ack items.</summary>
    public Guid? SourceAppraisalDecisionId { get; private set; }

    public ItemDecision ItemDecision { get; private set; }
    public DateTime? DecisionAt { get; private set; }
    public string? DecisionBy { get; private set; }
    public string? DecisionReason { get; private set; }

    private MeetingItem()
    {
    }

    /// <summary>
    /// Creates a Decision item for a workflow-gated appraisal.
    /// Replaces the previous <c>Create</c> factory.
    /// </summary>
    internal static MeetingItem CreateDecision(
        Guid meetingId,
        Guid appraisalId,
        string? appraisalNo,
        decimal facilityLimit,
        string? appraisalType,
        Guid workflowInstanceId,
        string activityId)
    {
        return new MeetingItem
        {
            //Id = Guid.CreateVersion7(),
            MeetingId = meetingId,
            AppraisalId = appraisalId,
            AppraisalNo = appraisalNo,
            FacilityLimit = facilityLimit,
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            AddedAt = DateTime.Now,
            Kind = MeetingItemKind.Decision,
            AppraisalType = appraisalType,
            ItemDecision = ItemDecision.Pending
        };
    }

    /// <summary>
    /// Creates an Acknowledgement item (no workflow gate; information only).
    /// </summary>
    internal static MeetingItem CreateAcknowledgement(
        Guid meetingId,
        Guid appraisalId,
        string? appraisalNo,
        decimal facilityLimit,
        string? appraisalType,
        string acknowledgementGroup,
        Guid sourceAppraisalDecisionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acknowledgementGroup);

        return new MeetingItem
        {
            Id = Guid.CreateVersion7(),
            MeetingId = meetingId,
            AppraisalId = appraisalId,
            AppraisalNo = appraisalNo,
            FacilityLimit = facilityLimit,
            WorkflowInstanceId = null,
            ActivityId = null,
            AddedAt = DateTime.Now,
            Kind = MeetingItemKind.Acknowledgement,
            AppraisalType = appraisalType,
            AcknowledgementGroup = acknowledgementGroup,
            SourceAppraisalDecisionId = sourceAppraisalDecisionId,
            ItemDecision = ItemDecision.Pending
        };
    }

    /// <summary>
    /// Records a secretary decision (Release or RouteBack) on this item.
    /// Called by the Meeting aggregate — not directly from application code.
    /// </summary>
    internal void ApplyDecision(ItemDecision decision, string actor, string? reason, DateTime now)
    {
        ItemDecision = decision;
        DecisionAt = now;
        DecisionBy = actor;
        DecisionReason = reason;
    }
}
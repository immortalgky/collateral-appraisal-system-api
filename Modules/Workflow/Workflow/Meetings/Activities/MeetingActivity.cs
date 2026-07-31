using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Meetings.Activities;

/// <summary>
/// Pauses the workflow until a scheduled meeting involving this appraisal ends.
/// On execute: enqueues a MeetingQueueItem and publishes AppraisalAwaitingMeetingEvent.
/// Resume is triggered externally via IWorkflowEngine.ResumeWorkflowAsync when the
/// meeting is ended (or cancelled) by the MeetingSecretary.
/// </summary>
public class MeetingActivity : WorkflowActivityBase
{
    private readonly WorkflowDbContext _dbContext;
    private readonly IPublisher _publisher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<MeetingActivity> _logger;

    public MeetingActivity(
        WorkflowDbContext dbContext,
        IPublisher publisher,
        IDateTimeProvider dateTimeProvider,
        ILogger<MeetingActivity> logger)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public override string ActivityType => ActivityTypes.MeetingActivity;
    public override string Name => "Meeting";
    public override string Description => "Waits for an approval meeting scheduled by the Meeting Secretary";

    protected override async Task<ActivityResult> ExecuteActivityAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var appraisalId = GetVariable<Guid>(context, "appraisalId");
            if (appraisalId == Guid.Empty)
                return ActivityResult.Failed("appraisalId variable is required for MeetingActivity");

            var facilityLimit = GetVariable<decimal>(context, "facilityLimit");
            var appraisalValue = GetVariable<decimal>(context, "appraisalValue");
            var appraisalNo = GetVariable<string?>(context, "appraisalNumber");

            // Re-entry path: if this appraisal+workflowInstance already has a non-Released Decision
            // item on a still-live meeting — RoutedBack (rework) or Pending (a secretary recall) —
            // re-enter that same meeting instead of enqueueing a new queue row. Excludes Cancelled
            // meetings: Meeting.Cancel() does not remove its items, so a stale Pending/RoutedBack row
            // left behind by a cancel-and-reschedule must not win over the live meeting's row.
            // OrderByDescending(AddedAt) makes the pick deterministic if more than one such row
            // somehow exists.
            //
            // This predicate depends on the workflow engine having already flushed the Meeting
            // aggregate's reset (Released → Pending on recall, or the RoutedBack reinstate) to the
            // database before this activity runs. The engine checkpoints (persists) workflow state
            // after every activity completes and before the next one executes, so by the time
            // MeetingActivity re-enters here that write has already landed — this predicate would
            // otherwise see stale data.
            var existingItem = await _dbContext.MeetingItems
                .Where(mi =>
                    mi.AppraisalId == appraisalId &&
                    mi.WorkflowInstanceId == context.WorkflowInstanceId &&
                    mi.Kind == MeetingItemKind.Decision &&
                    mi.ItemDecision != ItemDecision.Released &&
                    _dbContext.Meetings.Any(m => m.Id == mi.MeetingId && m.Status != MeetingStatus.Cancelled))
                .OrderByDescending(mi => mi.AddedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingItem is not null)
            {
                var meeting = await _dbContext.Meetings
                    .Include(m => m.Items)
                    .FirstOrDefaultAsync(m => m.Id == existingItem.MeetingId, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Meeting {existingItem.MeetingId} not found for existing item {existingItem.Id}");

                if (existingItem.ItemDecision == ItemDecision.RoutedBack)
                {
                    meeting.ReinstateRoutedBackItem(appraisalId, _dateTimeProvider.ApplicationNow);

                    _logger.LogInformation(
                        "MeetingActivity {ActivityId} reinstated appraisal {AppraisalId} on meeting {MeetingId} after rework",
                        context.ActivityId, appraisalId, meeting.Id);
                }
                else
                {
                    // Pending: the recall command already reset the item back to Pending on the
                    // Meeting aggregate — nothing further to do here.
                    _logger.LogInformation(
                        "MeetingActivity {ActivityId} re-entered for appraisal {AppraisalId} on meeting {MeetingId} after a recall",
                        context.ActivityId, appraisalId, meeting.Id);
                }

                return ActivityResult.Pending(new Dictionary<string, object>
                {
                    [$"{NormalizeActivityId(context.ActivityId)}_awaitingMeeting"] = true,
                    [$"{NormalizeActivityId(context.ActivityId)}_reenteredOnMeetingId"] = meeting.Id
                });
            }

            // Normal enqueue path: first time through MeetingActivity for this appraisal+workflow.
            // Idempotent: if a non-Released queue row already exists, reuse it.
            var existing = await _dbContext.MeetingQueueItems
                .FirstOrDefaultAsync(q =>
                    q.AppraisalId == appraisalId &&
                    q.WorkflowInstanceId == context.WorkflowInstanceId &&
                    q.Status != MeetingQueueItemStatus.Released, cancellationToken);

            if (existing is null)
            {
                var queueItem = MeetingQueueItem.CreateQueued(
                    appraisalId,
                    appraisalNo,
                    facilityLimit,
                    appraisalValue,
                    context.WorkflowInstanceId,
                    context.ActivityId);
                _dbContext.MeetingQueueItems.Add(queueItem);
            }

            // Publish-before-save is consistent with TaskActivity/ApprovalActivity:
            // the workflow engine commits activity state + enqueued MeetingQueueItem together
            // after ExecuteActivityAsync returns. MediatR in-process handlers run synchronously
            // on the same DbContext, so they observe the pre-commit state.
            await _publisher.Publish(new AppraisalAwaitingMeetingEvent(
                appraisalId,
                appraisalNo,
                facilityLimit,
                appraisalValue,
                context.WorkflowInstanceId,
                context.ActivityId), cancellationToken);

            _logger.LogInformation(
                "MeetingActivity {ActivityId} enqueued appraisal {AppraisalId} for meeting (facilityLimit={FacilityLimit}, appraisalValue={AppraisalValue})",
                context.ActivityId, appraisalId, facilityLimit, appraisalValue);

            var outputData = new Dictionary<string, object>
            {
                [$"{NormalizeActivityId(context.ActivityId)}_awaitingMeeting"] = true,
                [$"{NormalizeActivityId(context.ActivityId)}_enqueuedAt"] = _dateTimeProvider.ApplicationNow
            };

            return ActivityResult.Pending(outputData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MeetingActivity {ActivityId} execution failed", context.ActivityId);
            return ActivityResult.Failed($"MeetingActivity failed: {ex.Message}");
        }
    }

    protected override Task<ActivityResult> ResumeActivityAsync(
        ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        // Resume is triggered externally after the meeting ends (or is cancelled).
        // The outcome is carried in resumeInput so downstream transitions can route.
        var normalized = NormalizeActivityId(context.ActivityId);
        var outputData = new Dictionary<string, object>();

        if (resumeInput.TryGetValue("meetingId", out var meetingId))
            outputData[$"{normalized}_meetingId"] = meetingId;
        if (resumeInput.TryGetValue("meetingOutcome", out var outcome))
        {
            outputData[$"{normalized}_meetingOutcome"] = outcome;
            outputData["meetingOutcome"] = outcome;
        }
        if (resumeInput.TryGetValue("cancelReason", out var reason))
            outputData[$"{normalized}_cancelReason"] = reason;

        // Propagate meeting member user IDs so downstream ApprovalActivity can consume
        // them as its approver list (released items only).
        if (resumeInput.TryGetValue("meetingMemberUserIds", out var memberIds))
        {
            outputData[$"{normalized}_meetingMemberUserIds"] = memberIds;
            outputData["meetingMemberUserIds"] = memberIds;
        }

        // Propagate override member list so downstream ApprovalActivity can replace its committee members.
        if (resumeInput.TryGetValue("meetingMemberOverrides", out var memberOverrides))
        {
            outputData[$"{normalized}_meetingMemberOverrides"] = memberOverrides;
            outputData["meetingMemberOverrides"] = memberOverrides;
        }

        if (resumeInput.TryGetValue("routeBackReason", out var routeBackReason))
            outputData[$"{normalized}_routeBackReason"] = routeBackReason;

        if (resumeInput.TryGetValue("completedBy", out var completedBy))
            outputData[$"{normalized}_completedBy"] = completedBy;

        return Task.FromResult(ActivityResult.Success(outputData));
    }
}

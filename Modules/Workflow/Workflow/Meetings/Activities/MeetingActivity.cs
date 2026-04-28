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
            var appraisalNo = GetVariable<string?>(context, "appraisalNumber");

            // Re-entry path: if this appraisal+workflowInstance already has a RoutedBack item
            // on a meeting, reinstate it on that same meeting instead of enqueueing a new one.
            var routedBackItem = await _dbContext.MeetingItems
                .FirstOrDefaultAsync(mi =>
                    mi.AppraisalId == appraisalId &&
                    mi.WorkflowInstanceId == context.WorkflowInstanceId &&
                    mi.Kind == MeetingItemKind.Decision &&
                    mi.ItemDecision == ItemDecision.RoutedBack, cancellationToken);

            if (routedBackItem is not null)
            {
                var meeting = await _dbContext.Meetings
                    .Include(m => m.Items)
                    .FirstOrDefaultAsync(m => m.Id == routedBackItem.MeetingId, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Meeting {routedBackItem.MeetingId} not found for routed-back item {routedBackItem.Id}");

                meeting.ReinstateRoutedBackItem(appraisalId, _dateTimeProvider.ApplicationNow);

                _logger.LogInformation(
                    "MeetingActivity {ActivityId} reinstated appraisal {AppraisalId} on meeting {MeetingId} after rework",
                    context.ActivityId, appraisalId, meeting.Id);

                return ActivityResult.Pending(new Dictionary<string, object>
                {
                    [$"{NormalizeActivityId(context.ActivityId)}_awaitingMeeting"] = true,
                    [$"{NormalizeActivityId(context.ActivityId)}_reinstatedOnMeetingId"] = meeting.Id
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
                context.WorkflowInstanceId,
                context.ActivityId), cancellationToken);

            _logger.LogInformation(
                "MeetingActivity {ActivityId} enqueued appraisal {AppraisalId} for meeting (facilityLimit={FacilityLimit})",
                context.ActivityId, appraisalId, facilityLimit);

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

        if (resumeInput.TryGetValue("routeBackReason", out var routeBackReason))
            outputData[$"{normalized}_routeBackReason"] = routeBackReason;

        if (resumeInput.TryGetValue("completedBy", out var completedBy))
            outputData[$"{normalized}_completedBy"] = completedBy;

        return Task.FromResult(ActivityResult.Success(outputData));
    }
}

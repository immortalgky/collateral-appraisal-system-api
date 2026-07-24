using FluentValidation;
using Shared.Identity;
using Workflow.Meetings.Domain;
using Workflow.Meetings.ReadModels;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.Meetings.Features.RecallMeetingItem;

public class RecallMeetingItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/items/{appraisalId:guid}/recall", async (
                Guid id,
                Guid appraisalId,
                RecallMeetingItemRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new RecallMeetingItemCommand(id, appraisalId, request.Reason, request.Force), ct);
                return Results.NoContent();
            })
            .WithName("RecallMeetingItem")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingSecretary")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record RecallMeetingItemRequest(string Reason, bool Force = false);

public record RecallMeetingItemCommand(Guid MeetingId, Guid AppraisalId, string Reason, bool Force)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class RecallMeetingItemCommandValidator : AbstractValidator<RecallMeetingItemCommand>
{
    public RecallMeetingItemCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

public class RecallMeetingItemCommandHandler(
    IMeetingRepository meetingRepository,
    WorkflowDbContext dbContext,
    IWorkflowActivityExecutionRepository activityExecutionRepository,
    IWorkflowPersistenceService workflowPersistenceService,
    IApprovalVoteRepository voteRepository,
    IWorkflowService workflowService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ILogger<RecallMeetingItemCommandHandler> logger)
    : ICommandHandler<RecallMeetingItemCommand>
{
    public async Task<Unit> Handle(RecallMeetingItemCommand command, CancellationToken ct)
    {
        var actor = currentUserService.Username
            ?? throw new InvalidOperationException("User is not authenticated");

        var meeting = await meetingRepository.GetByIdForDecisionAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        var item = meeting.Items.FirstOrDefault(i =>
                i.AppraisalId == command.AppraisalId && i.Kind == MeetingItemKind.Decision)
            ?? throw new NotFoundException($"Appraisal {command.AppraisalId} is not on meeting {command.MeetingId}");

        if (item.ItemDecision != ItemDecision.Released)
            throw new ConflictException(
                $"Appraisal {command.AppraisalId} is not a released decision item and cannot be recalled",
                "RECALL_NOT_RELEASED");

        var workflowInstanceId = item.WorkflowInstanceId!.Value;

        // Serialize against a concurrent committee vote on the same approval round BEFORE reading
        // vote state below — otherwise a vote committing in that window would be silently discarded
        // once we reset the round. Same lock resource WorkflowService's approval-resume path
        // acquires ($"wf-approval:{instanceId}"); held by this command's ambient transaction, so the
        // later acquisition inside ResumeWorkflowAsync (when the recall event resumes the workflow)
        // is a re-entrant no-op on the same connection/transaction.
        var lockResult = await workflowPersistenceService.AcquireApplicationLockAsync(
            $"wf-approval:{workflowInstanceId}", "Exclusive", 30000, ct);
        if (lockResult < 0)
            throw new ConflictException(
                $"Approval for appraisal {command.AppraisalId} is busy (lock code {lockResult}); please retry.",
                "RECALL_BUSY");

        // The approval round must still be sitting on pending-approval — if it already
        // resolved (approve/reject/route_back), the workflow has moved on and the release
        // is no longer recallable.
        var execution = await activityExecutionRepository.GetCurrentActivityForWorkflowAsync(
            workflowInstanceId, ct);
        if (execution is null || execution.ActivityId != "pending-approval")
            throw new ConflictException(
                $"Appraisal {command.AppraisalId} is no longer awaiting committee approval and cannot be recalled",
                "RECALL_ALREADY_RESOLVED");

        // NOTE: workflow instances are pinned to the WorkflowDefinitionVersion they started on,
        // and a version predating the recall-to-meeting transition would match no transition on
        // resume — which the engine treats as "workflow finished" (WorkflowEngine: null next
        // activity => CompleteWorkflowAsync). There is deliberately no guard for that here:
        // in-flight instances are migrated to a recall-capable version out of band before this
        // feature is enabled.
        //
        // Blocked once any committee member has voted; the secretary can override with force
        // (and a reason — already required by the validator) rather than a second permission.
        var hasVotes = await voteRepository.HasAnyVoteAsync(execution.Id, ct);
        if (hasVotes)
        {
            if (!command.Force)
                throw new ConflictException(
                    $"At least one committee member has already voted on appraisal {command.AppraisalId}; recall requires force",
                    "RECALL_VOTES_EXIST");

            // The only audit trail for discarding real approver votes — record who, what, and why.
            logger.LogWarning(
                "RecallMeetingItem: {Actor} force-recalled appraisal {AppraisalId} on meeting {MeetingId} " +
                "despite existing votes. Reason: {Reason}",
                actor, command.AppraisalId, command.MeetingId, command.Reason);
        }

        var queueItem = await dbContext.MeetingQueueItems
            .FirstOrDefaultAsync(q =>
                q.AppraisalId == command.AppraisalId &&
                q.MeetingId == command.MeetingId &&
                q.Status == MeetingQueueItemStatus.Released, ct);
        if (queueItem is null)
            logger.LogWarning(
                "RecallMeetingItem: no Released MeetingQueueItem found for appraisal {AppraisalId} on meeting " +
                "{MeetingId}; the queue view will diverge from the meeting until reconciled",
                command.AppraisalId, command.MeetingId);
        else
            queueItem.Requeue();

        meeting.UndoRelease(command.AppraisalId, actor, command.Reason, dateTimeProvider.ApplicationNow);

        // Flush the Requeue + the item's Released -> Pending BEFORE resuming the workflow, so the
        // recall gate in ApprovalActivity reads persisted state. This runs inside the command's
        // ambient transaction; it is not a commit. The resume must be inline (not via a domain
        // event) because DispatchDomainEventInterceptor dispatches events during SaveChanges,
        // before the flush — an event-driven resume would therefore run pre-flush and the gate
        // would still see the committed 'Released' row.
        await dbContext.SaveChangesAsync(ct);

        // Resume the parked pending-approval activity with the recall decision. The active-
        // transaction branch of ResumeWorkflowAsync runs in-line and re-acquires the
        // wf-approval lock re-entrantly on this same transaction.
        await workflowService.ResumeWorkflowAsync(
            workflowInstanceId,
            activityId: "pending-approval",
            completedBy: actor,
            input: new Dictionary<string, object>
            {
                ["decisionTaken"] = MeetingOutcomes.Recalled,
                ["completedBy"] = actor,
                ["comments"] = command.Reason
            },
            cancellationToken: ct);

        return Unit.Value;
    }
}

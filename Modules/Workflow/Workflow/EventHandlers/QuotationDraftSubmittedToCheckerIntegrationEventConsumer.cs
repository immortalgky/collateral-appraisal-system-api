using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Data.Repository;

namespace Workflow.EventHandlers;

/// <summary>
/// Reassigns the open PendingTask for the submitting company from ExtAdmin (Maker) to
/// ExtAppraisalChecker (Checker) when the Maker clicks "Submit to Checker" on their quotation draft.
///
/// Lookup: CorrelationId = QuotationRequestId, ActivityId = "ext-collect-submissions",
///         AssigneeCompanyId = CompanyId.
///
/// Idempotency:
///   - Task not found (pre-feature RFQ or wrong event) → log warning, ACK.
///   - Task already assigned to ExtAppraisalChecker → log info, ACK.
///   - Task is in a terminal state (Completing/Completed) → log info, ACK.
///   - Otherwise → Reassign and persist.
/// </summary>
public class QuotationDraftSubmittedToCheckerIntegrationEventConsumer(
    WorkflowDbContext dbContext,
    IAssignmentRepository assignmentRepository,
    InboxGuard<WorkflowDbContext> inboxGuard,
    ILogger<QuotationDraftSubmittedToCheckerIntegrationEventConsumer> logger)
    : IConsumer<QuotationDraftSubmittedToCheckerIntegrationEvent>
{
    private const string TargetRole = "ExtAppraisalChecker";
    private const string ActivityId = "ext-collect-submissions";

    public async Task Consume(ConsumeContext<QuotationDraftSubmittedToCheckerIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "QuotationDraftSubmittedToCheckerConsumer: QuotationRequestId={QuotationRequestId} CompanyId={CompanyId} SubmittedBy={SubmittedBy}",
            message.QuotationRequestId, message.CompanyId, message.SubmittedBy);

        var task = await assignmentRepository.GetFanOutTaskByCorrelationIdAndCompanyAsync(
            message.QuotationRequestId,
            ActivityId,
            message.CompanyId,
            ct);

        if (task is null)
        {
            logger.LogWarning(
                "QuotationDraftSubmittedToCheckerConsumer: no PendingTask found for QuotationRequestId={QuotationRequestId} CompanyId={CompanyId} — skipping",
                message.QuotationRequestId, message.CompanyId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        var status = task.TaskStatus.Code;
        if (status == TaskStatus.Completing.Code || status == TaskStatus.Completed.Code)
        {
            logger.LogInformation(
                "QuotationDraftSubmittedToCheckerConsumer: PendingTask {TaskId} is already terminal ({Status}) — skipping",
                task.Id, status);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        if (task.AssignedTo == TargetRole)
        {
            logger.LogInformation(
                "QuotationDraftSubmittedToCheckerConsumer: PendingTask {TaskId} already assigned to {Role} — idempotent re-delivery, skipping",
                task.Id, TargetRole);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        task.Reassign(TargetRole, "2");

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "QuotationDraftSubmittedToCheckerConsumer: reassigned PendingTask {TaskId} to {Role} for QuotationRequestId={QuotationRequestId} CompanyId={CompanyId}",
            task.Id, TargetRole, message.QuotationRequestId, message.CompanyId);

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
    }
}

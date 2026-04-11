using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Workflow.Repositories;

namespace Workflow.EventHandlers;

/// <summary>
/// Handles AppraisalCreatedIntegrationEvent by recording the appraisalId
/// in the workflow instance's Variables. The workflow was already started
/// by RequestSubmittedIntegrationEventConsumer with correlationId = requestId.
/// </summary>
public class AppraisalCreatedIntegrationEventConsumer(
    ILogger<AppraisalCreatedIntegrationEventConsumer> logger,
    IWorkflowInstanceRepository workflowInstanceRepository,
    IWorkflowUnitOfWork unitOfWork,
    InboxGuard<WorkflowDbContext> inboxGuard) : IConsumer<AppraisalCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCreatedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, RequestId: {RequestId}",
            nameof(AppraisalCreatedIntegrationEvent),
            message.AppraisalId,
            message.RequestId);

        try
        {
            // Find workflow by correlationId = requestId
            var instance = await workflowInstanceRepository.GetByCorrelationId(
                message.RequestId.ToString(), context.CancellationToken);

            if (instance is null)
            {
                logger.LogWarning(
                    "No workflow found for RequestId: {RequestId}. AppraisalId {AppraisalId} will not be linked.",
                    message.RequestId, message.AppraisalId);
                // Mark processed so the inbox row doesn't remain 'Processing' forever.
                // No workflow exists, so there's nothing to retry against.
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
                return;
            }

            // Record appraisal details in workflow variables
            instance.UpdateVariables(new Dictionary<string, object>
            {
                ["appraisalId"] = message.AppraisalId,
                ["appraisalNumber"] = message.AppraisalNumber ?? string.Empty,
                ["appraisalType"] = message.AppraisalType ?? "Initial"
            });

            await unitOfWork.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation(
                "Linked AppraisalId {AppraisalId} to workflow {WorkflowInstanceId} for RequestId: {RequestId}",
                message.AppraisalId, instance.Id, message.RequestId);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error linking AppraisalId {AppraisalId} to workflow for RequestId: {RequestId}",
                message.AppraisalId, message.RequestId);
            throw;
        }
    }
}

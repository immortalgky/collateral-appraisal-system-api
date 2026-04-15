using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;
using static Workflow.Workflow.Services.WorkflowSignals;

namespace Workflow.EventHandlers;

public class AppraisalCreatedIntegrationEventConsumer(
    ILogger<AppraisalCreatedIntegrationEventConsumer> logger,
    IWorkflowInstanceRepository workflowInstanceRepository,
    IWorkflowSignalDispatcher signalDispatcher,
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
            var instance = await workflowInstanceRepository.GetByCorrelationId(
                message.RequestId.ToString(), context.CancellationToken);

            if (instance is null)
            {
                logger.LogWarning(
                    "No workflow found for RequestId: {RequestId}. AppraisalId {AppraisalId} will not be linked.",
                    message.RequestId, message.AppraisalId);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
                return;
            }

            var appraisalPayload = new Dictionary<string, object>
            {
                ["appraisalId"] = message.AppraisalId,
                ["appraisalNumber"] = message.AppraisalNumber ?? string.Empty,
                ["appraisalType"] = message.AppraisalType ?? "New"
            };

            // Wrap in execution strategy + transaction so that
            // SqlServerRetryingExecutionStrategy allows DB operations inside
            // the user-initiated transaction, and ResumeWorkflowAsync takes
            // the HasActiveTransaction branch to defer commit.
            var strategy = unitOfWork.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await unitOfWork.BeginTransactionAsync(context.CancellationToken);
                try
                {
                    instance.UpdateVariables(appraisalPayload);

                    await signalDispatcher.DispatchAsync(
                        signalName: AppraisalCreated,
                        correlationValue: message.RequestId.ToString(),
                        payload: appraisalPayload,
                        context.CancellationToken);

                    await unitOfWork.SaveChangesAsync(context.CancellationToken);
                    await unitOfWork.CommitTransactionAsync(context.CancellationToken);
                }
                catch
                {
                    try { await unitOfWork.RollbackTransactionAsync(context.CancellationToken); }
                    catch (Exception rollbackEx)
                    {
                        logger.LogError(rollbackEx, "Failed to rollback transaction for RequestId: {RequestId}",
                            message.RequestId);
                    }

                    throw;
                }
            });

            logger.LogInformation(
                "Linked AppraisalId {AppraisalId} to workflow {WorkflowInstanceId} and dispatched signal for RequestId: {RequestId}",
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

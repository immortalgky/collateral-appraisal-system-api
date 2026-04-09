using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.EventHandlers;

public class AppraisalCreatedIntegrationEventConsumer(
    ILogger<AppraisalCreatedIntegrationEventConsumer> logger,
    IWorkflowDefinitionRepository workflowDefinitionRepository,
    IWorkflowInstanceRepository workflowInstanceRepository,
    IWorkflowService workflowService,
    InboxGuard<WorkflowDbContext> inboxGuard) : IConsumer<AppraisalCreatedIntegrationEvent>
{
    private const string WorkflowName = "Collateral Appraisal Workflow";

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
            // Idempotency guard: skip if workflow already exists for this appraisal
            var existing = await workflowInstanceRepository.GetByCorrelationId(
                message.AppraisalId.ToString(), context.CancellationToken);
            if (existing is not null)
            {
                logger.LogInformation(
                    "Workflow already exists for AppraisalId: {AppraisalId} (InstanceId: {InstanceId}), skipping",
                    message.AppraisalId, existing.Id);
                return;
            }

            var definition = await workflowDefinitionRepository.GetLatestVersion(
                WorkflowName, context.CancellationToken);

            if (definition is null)
            {
                logger.LogError(
                    "Workflow definition '{WorkflowName}' not found. Cannot start workflow for AppraisalId: {AppraisalId}",
                    WorkflowName, message.AppraisalId);
                throw new InvalidOperationException(
                    $"Workflow definition '{WorkflowName}' not found.");
            }

            var instanceName = $"Appraisal-{message.AppraisalNumber ?? message.AppraisalId.ToString()}";

            var initialVariables = new Dictionary<string, object>
            {
                ["requestId"] = message.RequestId,
                ["appraisalId"] = message.AppraisalId,
                ["appraisalNumber"] = message.AppraisalNumber ?? string.Empty,
                ["appraisalType"] = message.AppraisalType ?? "Initial",
                ["isPma"] = message.IsPma,
                ["facilityLimit"] = message.FacilityLimit ?? 0m,
                ["priority"] = message.Priority ?? "normal",
                ["hasAppraisalBook"] = message.HasAppraisalBook,
                ["channel"] = message.Channel ?? string.Empty
            };

            var instance = await workflowService.StartWorkflowAsync(
                workflowDefinitionId: definition.Id,
                instanceName: instanceName,
                startedBy: message.CreatedBy ?? "system",
                initialVariables: initialVariables,
                correlationId: message.AppraisalId.ToString(),
                cancellationToken: context.CancellationToken);

            logger.LogInformation(
                "Successfully started workflow instance {WorkflowInstanceId} for AppraisalId: {AppraisalId}",
                instance.Id, message.AppraisalId);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error starting workflow for AppraisalId: {AppraisalId}",
                message.AppraisalId);

            // Let exception propagate for MassTransit retry/error handling
            throw;
        }
    }
}

using Shared.Messaging.Events;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.EventHandlers;

public class AppraisalCreatedIntegrationEventConsumer(
    ILogger<AppraisalCreatedIntegrationEventConsumer> logger,
    IWorkflowDefinitionRepository workflowDefinitionRepository,
    IWorkflowService workflowService) : IConsumer<AppraisalCreatedIntegrationEvent>
{
    private const string WorkflowName = "Collateral Appraisal Workflow";

    public async Task Consume(ConsumeContext<AppraisalCreatedIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, RequestId: {RequestId}",
            nameof(AppraisalCreatedIntegrationEvent),
            message.AppraisalId,
            message.RequestId);

        try
        {
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
                ["appraisalType"] = message.AppraisalType ?? "Initial"
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

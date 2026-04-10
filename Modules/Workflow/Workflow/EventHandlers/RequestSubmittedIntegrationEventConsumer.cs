using System.Text.Json;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.EventHandlers;

/// <summary>
/// Handles RequestSubmittedIntegrationEvent by immediately starting a workflow
/// and conditionally triggering appraisal creation based on table-driven configuration.
/// CorrelationId = requestId (not appraisalId).
/// </summary>
public class RequestSubmittedIntegrationEventConsumer(
    ILogger<RequestSubmittedIntegrationEventConsumer> logger,
    IWorkflowDefinitionRepository workflowDefinitionRepository,
    IWorkflowInstanceRepository workflowInstanceRepository,
    IWorkflowService workflowService,
    AppraisalCreationTriggerEvaluator triggerEvaluator,
    InboxGuard<WorkflowDbContext> inboxGuard) : IConsumer<RequestSubmittedIntegrationEvent>
{
    private const string WorkflowName = "Collateral Appraisal Workflow";

    public async Task Consume(ConsumeContext<RequestSubmittedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for RequestId: {RequestId}",
            nameof(RequestSubmittedIntegrationEvent), message.RequestId);

        try
        {
            // Idempotency: skip if workflow already exists for this request
            var existing = await workflowInstanceRepository.GetByCorrelationId(
                message.RequestId.ToString(), context.CancellationToken);
            if (existing is not null)
            {
                logger.LogInformation(
                    "Workflow already exists for RequestId: {RequestId} (InstanceId: {InstanceId}), skipping",
                    message.RequestId, existing.Id);
                return;
            }

            // Resolve workflow definition
            var definition = await workflowDefinitionRepository.GetLatestVersion(
                WorkflowName, context.CancellationToken)
                ?? throw new InvalidOperationException(
                    $"Workflow definition '{WorkflowName}' not found.");

            // Build initial variables
            var initialVariables = new Dictionary<string, object>
            {
                ["requestId"] = message.RequestId,
                ["channel"] = message.Channel ?? string.Empty,
                ["priority"] = message.Priority ?? "normal",
                ["isPma"] = message.IsPma,
                ["facilityLimit"] = message.FacilityLimit ?? 0m,
                ["hasAppraisalBook"] = message.HasAppraisalBook,
                ["requestSubmissionPayload"] = JsonSerializer.Serialize(message)
            };

            var instanceName = $"Request-{message.RequestId}";

            // Start workflow with correlationId = requestId
            var instance = await workflowService.StartWorkflowAsync(
                workflowDefinitionId: definition.Id,
                instanceName: instanceName,
                startedBy: message.CreatedBy ?? "system",
                initialVariables: initialVariables,
                correlationId: message.RequestId.ToString(),
                cancellationToken: context.CancellationToken);

            logger.LogInformation(
                "Started workflow instance {WorkflowInstanceId} for RequestId: {RequestId}",
                instance.Id, message.RequestId);

            // Evaluate table-driven trigger for immediate appraisal creation
            var shouldEmit = await triggerEvaluator.ShouldEmitAsync(
                "__on_workflow_start__", initialVariables, ct: context.CancellationToken);

            if (shouldEmit)
            {
                await context.Publish(new AppraisalCreationRequestedIntegrationEvent
                {
                    RequestId = message.RequestId,
                    RequestTitles = message.RequestTitles,
                    Appointment = message.Appointment,
                    Fee = message.Fee,
                    Contact = message.Contact,
                    CreatedBy = message.CreatedBy,
                    Priority = message.Priority,
                    IsPma = message.IsPma,
                    Purpose = message.Purpose,
                    Channel = message.Channel,
                    BankingSegment = message.BankingSegment,
                    FacilityLimit = message.FacilityLimit,
                    HasAppraisalBook = message.HasAppraisalBook,
                    RequestedBy = message.RequestedBy,
                    RequestedAt = message.RequestedAt
                }, context.CancellationToken);

                logger.LogInformation(
                    "Published AppraisalCreationRequestedIntegrationEvent for RequestId: {RequestId} (immediate path)",
                    message.RequestId);
            }
            else
            {
                logger.LogInformation(
                    "Appraisal creation deferred for RequestId: {RequestId} (channel: {Channel})",
                    message.RequestId, message.Channel);
            }

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing RequestSubmittedIntegrationEvent for RequestId: {RequestId}",
                message.RequestId);
            throw;
        }
    }
}

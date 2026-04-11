using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Workflow.Models;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.EventHandlers;

/// <summary>
/// Handles RequestSubmittedIntegrationEvent by immediately starting a workflow
/// and conditionally triggering appraisal creation based on table-driven configuration.
/// CorrelationId = requestId (not appraisalId).
///
/// Atomicity: uses IIntegrationEventOutbox so that workflow creation and the
/// AppraisalCreationRequestedIntegrationEvent are committed in the same transaction.
/// A crash between workflow creation and event publish cannot happen — either both
/// persist or neither does.
/// </summary>
public class RequestSubmittedIntegrationEventConsumer(
    ILogger<RequestSubmittedIntegrationEventConsumer> logger,
    IWorkflowDefinitionRepository workflowDefinitionRepository,
    IWorkflowInstanceRepository workflowInstanceRepository,
    IWorkflowService workflowService,
    IWorkflowUnitOfWork unitOfWork,
    IIntegrationEventOutbox outbox,
    AppraisalCreationTriggerEvaluator triggerEvaluator,
    InboxGuard<WorkflowDbContext> inboxGuard) : IConsumer<RequestSubmittedIntegrationEvent>
{
    private const string WorkflowName = "Collateral Appraisal Workflow";

    public async Task Consume(ConsumeContext<RequestSubmittedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for RequestId: {RequestId}",
            nameof(RequestSubmittedIntegrationEvent), message.RequestId);

        try
        {
            // Retry path: check if workflow already exists for this request
            var existing = await workflowInstanceRepository.GetByCorrelationId(
                message.RequestId.ToString(), ct);

            if (existing is not null)
            {
                await HandleExistingWorkflowAsync(existing, message, ct);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            // First run: start a new workflow, atomically publishing the
            // AppraisalCreationRequestedIntegrationEvent via the outbox if the
            // table-driven condition matches.
            var initialVariables = BuildInitialVariables(message);

            var shouldEmit = await triggerEvaluator.ShouldEmitAsync(
                "__on_workflow_start__", initialVariables, ct: ct);

            if (shouldEmit)
            {
                outbox.Publish(BuildCreationRequestedEvent(message), message.RequestId.ToString());
                initialVariables["appraisalCreationRequested"] = true;
            }

            var definition = await workflowDefinitionRepository.GetLatestVersion(WorkflowName, ct)
                ?? throw new InvalidOperationException(
                    $"Workflow definition '{WorkflowName}' not found.");

            var instance = await workflowService.StartWorkflowAsync(
                workflowDefinitionId: definition.Id,
                instanceName: $"Request-{message.RequestId}",
                startedBy: message.CreatedBy ?? "system",
                initialVariables: initialVariables,
                correlationId: message.RequestId.ToString(),
                cancellationToken: ct);

            logger.LogInformation(
                "Started workflow instance {WorkflowInstanceId} for RequestId: {RequestId} (shouldEmit={ShouldEmit})",
                instance.Id, message.RequestId, shouldEmit);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing RequestSubmittedIntegrationEvent for RequestId: {RequestId}",
                message.RequestId);
            throw;
        }
    }

    private async Task HandleExistingWorkflowAsync(
        WorkflowInstance existing, RequestSubmittedIntegrationEvent message, CancellationToken ct)
    {
        // If appraisal creation was already requested (flag set in Variables), skip.
        if (IsAppraisalCreationRequested(existing.Variables))
        {
            logger.LogInformation(
                "Workflow and appraisal creation already handled for RequestId: {RequestId}",
                message.RequestId);
            return;
        }

        // Stale-reclaim retry after a crash between workflow creation and flag set.
        // Re-evaluate the trigger. For the immediate path we re-publish; for the deferred
        // path the pipeline step handles publishing later, so we do nothing here.
        var shouldEmit = await triggerEvaluator.ShouldEmitAsync(
            "__on_workflow_start__", existing.Variables, ct: ct);

        if (!shouldEmit)
        {
            logger.LogInformation(
                "Existing workflow {WorkflowInstanceId} for RequestId {RequestId}: deferred path, nothing to re-publish",
                existing.Id, message.RequestId);
            return;
        }

        outbox.Publish(BuildCreationRequestedEvent(message), message.RequestId.ToString());
        existing.UpdateVariables(new Dictionary<string, object>
        {
            ["appraisalCreationRequested"] = true
        });
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Re-published AppraisalCreationRequestedIntegrationEvent for existing workflow {WorkflowInstanceId}, RequestId: {RequestId}",
            existing.Id, message.RequestId);
    }

    private static Dictionary<string, object> BuildInitialVariables(RequestSubmittedIntegrationEvent message) =>
        new()
        {
            ["requestId"] = message.RequestId,
            ["channel"] = message.Channel ?? string.Empty,
            ["priority"] = message.Priority ?? "normal",
            ["isPma"] = message.IsPma,
            ["facilityLimit"] = message.FacilityLimit ?? 0m,
            ["hasAppraisalBook"] = message.HasAppraisalBook,
            ["requestSubmissionPayload"] = JsonSerializer.Serialize(message)
        };

    private static AppraisalCreationRequestedIntegrationEvent BuildCreationRequestedEvent(
        RequestSubmittedIntegrationEvent message) =>
        new()
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
        };

    private static bool IsAppraisalCreationRequested(Dictionary<string, object>? variables)
    {
        if (variables is null || !variables.TryGetValue("appraisalCreationRequested", out var flag))
            return false;

        return flag switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var parsed) && parsed,
            JsonElement je => je.ValueKind == JsonValueKind.True,
            _ => false
        };
    }
}

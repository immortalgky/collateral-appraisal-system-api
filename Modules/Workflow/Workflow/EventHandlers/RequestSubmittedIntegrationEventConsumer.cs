using Appraisal.Contracts.Appraisals;
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
    ISender mediator,
    InboxGuard<WorkflowDbContext> inboxGuard) : IConsumer<RequestSubmittedIntegrationEvent>
{
    private const string WorkflowName = "Collateral Appraisal Workflow";

    // Appeal is request Purpose "12"; Progressive (construction inspection) is signalled by the
    // derived AppraisalType. Both reference a prior appraisal and require PrevAppraisalId.
    private const string AppealPurposeCode = "12";
    private const string ProgressiveAppraisalType = "Progressive";

    public Task Consume(ConsumeContext<RequestSubmittedIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<RequestSubmittedIntegrationEvent> context)
    {
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
                return;
            }

            // First run: start a new workflow, atomically publishing the
            // AppraisalCreationRequestedIntegrationEvent via the outbox if the
            // table-driven condition matches.
            var initialVariables = BuildInitialVariables(message);

            // Appeal and Progressive (construction inspection) both reference a prior appraisal and
            // require PrevAppraisalId. Resolve the latest appraisal in the chain the prior appraisal
            // belongs to, walked over appraisal.Appraisals.PrevAppraisalId. Appeal EXCLUDES that
            // company (of the tip); Progressive FORCES the company of the PICKED appraisal.
            //
            // The appraisal to copy from is always the one the user PICKED, for every purpose — the same
            // PrevAppraisalId the deferred path (EmitAppraisalCreationRequestedStep) sends. Progressive no
            // longer swaps in the chain tip: the submit guard already rejects a pick that has a later
            // inspection, so what is picked is what the next inspection continues from — and the
            // copy base, the fee source and the forced company are all that one appraisal.
            var resolvedSourceAppraisalId = message.PrevAppraisalId;

            var isAppeal = message.Purpose == AppealPurposeCode;
            var isProgressive = message.AppraisalType == ProgressiveAppraisalType;

            if (isAppeal || isProgressive)
            {
                if (!message.PrevAppraisalId.HasValue)
                {
                    logger.LogWarning(
                        "{Flow} request {RequestId} has no PrevAppraisalId; skipping prior-company resolution.",
                        isProgressive ? "Progressive" : "Appeal", message.RequestId);
                }
                else
                {
                    var prior = await mediator.Send(
                        new ResolveLatestInAppraisalChainQuery(message.PrevAppraisalId.Value), ct);

                    if (prior is null)
                    {
                        // The prior appraisal does not exist or is soft-deleted. Submission is gated
                        // on it being Completed, so this should be unreachable — log rather than
                        // proceed silently, and fall back to the raw PrevAppraisalId.
                        logger.LogWarning(
                            "{Flow} request {RequestId}: prior appraisal {PrevAppraisalId} did not resolve to a chain; "
                            + "using the raw PrevAppraisalId and leaving company selection to the normal rules.",
                            isProgressive ? "Progressive" : "Appeal", message.RequestId, message.PrevAppraisalId);
                    }
                    else if (isProgressive)
                    {
                        // A company-less pick is an internally-appraised case: there is no external
                        // company to carry over, so selection follows the normal rules.
                        if (prior.PickedCompanyId is { } progressiveCompanyId)
                        {
                            initialVariables["forceCompanyId"] = progressiveCompanyId.ToString();
                            initialVariables["forceCompanyName"] = prior.PickedCompanyName;
                        }
                        else
                        {
                            logger.LogInformation(
                                "Progressive request {RequestId}: picked appraisal {AppraisalId} has no external company; "
                                + "not forcing a company.",
                                message.RequestId, message.PrevAppraisalId);
                        }
                    }
                    else if (prior.CompanyId is { } appealCompanyId) // appeal
                    {
                        // KNOWN LIMITATION, carried over unchanged from the engagement-based lookup:
                        // this excludes the company that did the CHAIN TIP, not the one that did the
                        // appraisal actually being appealed. Where a later re-appraisal by a different
                        // company sits on the same chain, the disputed company is not the one excluded.
                        // Deliberately not changed here — this PR swaps the data source, it does not
                        // redesign appeal routing.
                        initialVariables["excludedCompanyId"] = appealCompanyId.ToString();
                    }
                }
            }

            // Resolve the workflow definition up-front so its id can be carried on the
            // AppraisalCreationRequestedIntegrationEvent — the Appraisal module needs it to
            // resolve the workflow-scope SLA budget and stamp the appraisal-level SLA fields.
            var definition = await workflowDefinitionRepository.GetLatestVersion(WorkflowName, ct)
                             ?? throw new InvalidOperationException(
                                 $"Workflow definition '{WorkflowName}' not found.");

            var shouldEmit = await triggerEvaluator.ShouldEmitAsync(
                "__on_workflow_start__", initialVariables, ct: ct);

            if (shouldEmit)
            {
                outbox.Publish(
                    BuildCreationRequestedEvent(message, resolvedSourceAppraisalId, definition.Id),
                    message.RequestId.ToString());
                initialVariables["appraisalCreationRequested"] = true;
            }

            var instance = await workflowService.StartWorkflowAsync(
                definition.Id,
                $"Request-{message.RequestId}",
                message.CreatedBy ?? "system",
                initialVariables,
                message.RequestId.ToString(),
                cancellationToken: ct);

            logger.LogInformation(
                "Started workflow instance {WorkflowInstanceId} for RequestId: {RequestId} (shouldEmit={ShouldEmit})",
                instance.Id, message.RequestId, shouldEmit);
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

        // The picked appraisal, as on the first run (see HandleAsync) — no chain resolution to redo.
        outbox.Publish(
            BuildCreationRequestedEvent(message, message.PrevAppraisalId, existing.WorkflowDefinitionId),
            message.RequestId.ToString());
        existing.UpdateVariables(new Dictionary<string, object>
        {
            ["appraisalCreationRequested"] = true
        });
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Re-published AppraisalCreationRequestedIntegrationEvent for existing workflow {WorkflowInstanceId}, RequestId: {RequestId}",
            existing.Id, message.RequestId);
    }

    private static Dictionary<string, object> BuildInitialVariables(RequestSubmittedIntegrationEvent message)
    {
        return new Dictionary<string, object>
        {
            ["requestId"] = message.RequestId,
            ["channel"] = message.Channel ?? string.Empty,
            ["entrySource"] = message.EntrySource ?? string.Empty,
            ["priority"] = message.Priority ?? "normal",
            ["isPma"] = message.IsPma,
            ["facilityLimit"] = message.FacilityLimit ?? 0m,
            ["hasAppraisalBook"] = message.HasAppraisalBook,
            ["bankingSegment"] = message.BankingSegment ?? string.Empty,
            ["requestSubmissionPayload"] = JsonSerializer.Serialize(message)
        };
    }

    private static AppraisalCreationRequestedIntegrationEvent BuildCreationRequestedEvent(
        RequestSubmittedIntegrationEvent message,
        Guid? resolvedPrevAppraisalId,
        Guid workflowDefinitionId)
    {
        return new AppraisalCreationRequestedIntegrationEvent
        {
            WorkflowDefinitionId = workflowDefinitionId,
            RequestId = message.RequestId,
            RequestTitles = message.RequestTitles,
            RequestProperties = message.RequestProperties,
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
            RequestedAt = message.RequestedAt,
            PrevAppraisalId = resolvedPrevAppraisalId,
            PrevAppraisalNumber = message.PrevAppraisalNumber,
            AppraisalType = message.AppraisalType,
            GroupTag = message.GroupTag
        };
    }

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
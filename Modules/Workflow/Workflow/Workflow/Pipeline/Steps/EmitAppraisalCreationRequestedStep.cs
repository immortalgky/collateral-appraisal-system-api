using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Pipeline step that publishes AppraisalCreationRequestedIntegrationEvent via the outbox.
/// Used for the deferred (manual channel) path during activity completion.
/// Delegates condition evaluation to AppraisalCreationTriggerEvaluator.
/// </summary>
public class EmitAppraisalCreationRequestedStep(
    IIntegrationEventOutbox outbox,
    AppraisalCreationTriggerEvaluator triggerEvaluator,
    ILogger<EmitAppraisalCreationRequestedStep> logger) : IActivityProcessStep
{
    public sealed record Parameters
    {
        /// <summary>Optional condition expression evaluated by AppraisalCreationTriggerEvaluator.</summary>
        public string? Condition { get; init; }

        /// <summary>Whether a decision field is required before triggering creation.</summary>
        public bool RequireDecision { get; init; }

        /// <summary>Decision field name when RequireDecision is true.</summary>
        public string? DecisionField { get; init; }
    }

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "EmitAppraisalCreationRequested",
        displayName: "Emit Appraisal Creation Requested",
        kind: StepKind.Action,
        description: "Publishes an AppraisalCreationRequestedIntegrationEvent via the outbox (deferred/manual channel).");

    public Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        // Build the raw variables/input dictionaries the legacy evaluator expects
        var variables = ctx.Variables.ToDictionary(kv => kv.Key, kv => kv.Value ?? (object)"");
        var input = ctx.Input.ToDictionary(kv => kv.Key, kv => kv.Value ?? (object)"");

        if (variables is null)
            return Task.FromResult(ProcessStepResult.Fail("NULL_VARIABLES", "Workflow variables are null"));

        // B5: Idempotency guard — check both the committed snapshot and any pending write
        // so that a re-run within the same pipeline pass is also deduplicated.
        var alreadyRequested =
            (variables.TryGetValue("appraisalCreationRequested", out var flagObj) && IsTruthy(flagObj)) ||
            (ctx.PendingVariableWrites.TryGetValue("appraisalCreationRequested", out var pendingFlag) &&
             IsTruthy(pendingFlag));

        if (alreadyRequested)
        {
            logger.LogInformation("Appraisal creation already requested for workflow {WorkflowInstanceId}, skipping",
                ctx.WorkflowInstanceId);
            return Task.FromResult(ProcessStepResult.Pass());
        }

        if (string.IsNullOrWhiteSpace(ctx.ParametersJson))
            return Task.FromResult(ProcessStepResult.Fail("MISSING_PARAMETERS", "Missing step parameters"));

        try
        {
            if (!triggerEvaluator.EvaluateConfig(ctx.ParametersJson, variables, input))
            {
                logger.LogDebug("Trigger condition not met for workflow {WorkflowInstanceId}", ctx.WorkflowInstanceId);
                return Task.FromResult(ProcessStepResult.Pass());
            }

            if (!variables.TryGetValue("requestSubmissionPayload", out var payloadObj))
                return Task.FromResult(ProcessStepResult.Fail(
                    "MISSING_PAYLOAD", "Missing requestSubmissionPayload in workflow variables"));

            var payloadJson = payloadObj switch
            {
                string s => s,
                JsonElement je => je.GetString() ?? je.GetRawText(),
                _ => payloadObj?.ToString()
            };

            if (string.IsNullOrWhiteSpace(payloadJson))
                return Task.FromResult(ProcessStepResult.Fail("EMPTY_PAYLOAD", "requestSubmissionPayload is empty"));

            var source = JsonSerializer.Deserialize<RequestSubmittedIntegrationEvent>(payloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (source is null)
                return Task.FromResult(ProcessStepResult.Fail(
                    "DESERIALIZE_FAILED", "Failed to deserialize requestSubmissionPayload"));

            outbox.Publish(new AppraisalCreationRequestedIntegrationEvent
            {
                RequestId = source.RequestId,
                RequestTitles = source.RequestTitles,
                Appointment = source.Appointment,
                Fee = source.Fee,
                Contact = source.Contact,
                CreatedBy = source.CreatedBy,
                Priority = source.Priority,
                IsPma = source.IsPma,
                Purpose = source.Purpose,
                Channel = source.Channel,
                BankingSegment = source.BankingSegment,
                FacilityLimit = source.FacilityLimit,
                HasAppraisalBook = source.HasAppraisalBook,
                RequestedBy = source.RequestedBy,
                RequestedAt = source.RequestedAt
            }, source.RequestId.ToString());

            // B5: Queue the write via SetVariable so it is persisted to the WorkflowInstance
            // by the pipeline after all Actions succeed.
            ctx.SetVariable("appraisalCreationRequested", true);

            logger.LogInformation(
                "Published AppraisalCreationRequestedIntegrationEvent for RequestId {RequestId}",
                source.RequestId);

            return Task.FromResult(ProcessStepResult.Pass());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to emit appraisal creation request for workflow {WorkflowInstanceId}",
                ctx.WorkflowInstanceId);
            return Task.FromResult(ProcessStepResult.Error(ex));
        }
    }

    private static bool IsTruthy(object? value) => value switch
    {
        bool b => b,
        string s => bool.TryParse(s, out var parsed) && parsed,
        JsonElement je => je.ValueKind == JsonValueKind.True,
        _ => false
    };
}

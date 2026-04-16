using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

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
    public string Name => "EmitAppraisalCreationRequested";

    public Task<ProcessStepResult> ExecuteAsync(ProcessStepContext context, CancellationToken ct)
    {
        if (context.Variables is null)
            return Task.FromResult(ProcessStepResult.Fail("Workflow variables are null"));

        var variables = context.Variables;

        // Idempotent guard: skip if already triggered
        if (variables.TryGetValue("appraisalCreationRequested", out var flagObj) && IsTruthy(flagObj))
        {
            logger.LogInformation("Appraisal creation already requested for workflow {WorkflowInstanceId}, skipping",
                context.WorkflowInstanceId);
            return Task.FromResult(ProcessStepResult.Ok());
        }

        if (string.IsNullOrWhiteSpace(context.Parameters))
            return Task.FromResult(ProcessStepResult.Fail("Missing step parameters"));

        try
        {
            // Delegate condition + requireDecision evaluation to shared helper
            if (!triggerEvaluator.EvaluateConfig(context.Parameters, variables, context.Input))
            {
                logger.LogDebug("Trigger condition not met for workflow {WorkflowInstanceId}", context.WorkflowInstanceId);
                return Task.FromResult(ProcessStepResult.Ok());
            }

            // Rehydrate the stashed request payload
            if (!variables.TryGetValue("requestSubmissionPayload", out var payloadObj))
                return Task.FromResult(ProcessStepResult.Fail("Missing requestSubmissionPayload in workflow variables"));

            var payloadJson = payloadObj switch
            {
                string s => s,
                JsonElement je => je.GetString() ?? je.GetRawText(),
                _ => payloadObj?.ToString()
            };

            if (string.IsNullOrWhiteSpace(payloadJson))
                return Task.FromResult(ProcessStepResult.Fail("requestSubmissionPayload is empty"));

            var source = JsonSerializer.Deserialize<RequestSubmittedIntegrationEvent>(payloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (source is null)
                return Task.FromResult(ProcessStepResult.Fail("Failed to deserialize requestSubmissionPayload"));

            // Publish via outbox (flushed on next SaveChangesAsync)
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

            // Set idempotency flag (mutates the tracked entity's Variables dictionary)
            variables["appraisalCreationRequested"] = true;

            logger.LogInformation(
                "Published AppraisalCreationRequestedIntegrationEvent for RequestId {RequestId} from workflow {WorkflowInstanceId}",
                source.RequestId, context.WorkflowInstanceId);

            return Task.FromResult(ProcessStepResult.Ok());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to emit appraisal creation request for workflow {WorkflowInstanceId}",
                context.WorkflowInstanceId);
            return Task.FromResult(ProcessStepResult.Fail(ex.Message));
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

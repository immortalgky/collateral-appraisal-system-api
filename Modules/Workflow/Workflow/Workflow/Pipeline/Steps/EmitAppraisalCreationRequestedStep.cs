using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Workflow.Engine.Expression;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Pipeline step that publishes AppraisalCreationRequestedIntegrationEvent via the outbox.
/// Used for the deferred (manual channel) path during activity completion.
/// Condition and requireDecision are read from the step's Parameters JSON.
/// </summary>
public class EmitAppraisalCreationRequestedStep(
    IIntegrationEventOutbox outbox,
    IExpressionEvaluator expressionEvaluator,
    ILogger<EmitAppraisalCreationRequestedStep> logger) : IActivityProcessStep
{
    public string Name => "EmitAppraisalCreationRequested";

    public Task<ProcessStepResult> ExecuteAsync(ProcessStepContext context, CancellationToken ct)
    {
        var variables = context.Variables ?? new Dictionary<string, object>();

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
            using var doc = JsonDocument.Parse(context.Parameters);
            var root = doc.RootElement;

            // Check requireDecision against input
            if (root.TryGetProperty("requireDecision", out var reqDecision))
            {
                var requiredValue = reqDecision.GetString();
                if (!string.IsNullOrEmpty(requiredValue))
                {
                    var decisionField = root.TryGetProperty("decisionField", out var df)
                        ? df.GetString() ?? "decisionTaken"
                        : "decisionTaken";

                    if (!context.Input.TryGetValue(decisionField, out var rawDecision))
                        return Task.FromResult(ProcessStepResult.Ok()); // No decision yet — not our turn

                    var actual = rawDecision switch
                    {
                        JsonElement je => je.GetString() ?? je.ToString(),
                        string s => s,
                        _ => rawDecision?.ToString()
                    };

                    if (!string.Equals(actual, requiredValue, StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult(ProcessStepResult.Ok()); // Different decision — not our turn
                }
            }

            // Evaluate condition expression
            if (root.TryGetProperty("condition", out var conditionElement))
            {
                var condition = conditionElement.GetString();
                if (!string.IsNullOrWhiteSpace(condition))
                {
                    var result = expressionEvaluator.EvaluateExpression(condition, variables);
                    if (!result)
                    {
                        logger.LogDebug("Condition '{Condition}' not met for workflow {WorkflowInstanceId}",
                            condition, context.WorkflowInstanceId);
                        return Task.FromResult(ProcessStepResult.Ok()); // Condition not met — not our turn
                    }
                }
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

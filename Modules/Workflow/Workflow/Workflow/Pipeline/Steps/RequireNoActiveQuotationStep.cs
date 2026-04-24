using System.Text.Json;
using Dapper;
using Shared.Data;
using Workflow.Data.Entities;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Validation step that blocks completion of the parent appraisal-assignment activity
/// while a quotation child workflow is still active (Status NOT IN Cancelled, Finalized, Closed).
///
/// Only applies when the activity definition carries <c>"canRaiseQuotation": true</c>
/// in its properties (set on the appraisal-assignment activity in appraisal-workflow.json).
/// </summary>
public class RequireNoActiveQuotationStep(
    IWorkflowInstanceRepository workflowInstanceRepository,
    ISqlConnectionFactory connectionFactory,
    ILogger<RequireNoActiveQuotationStep> logger) : IActivityProcessStep
{
    private static readonly string[] TerminalStatuses = ["Cancelled", "Finalized"];

    public sealed record Parameters;

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "RequireNoActiveQuotation",
        displayName: "Require No Active Quotation",
        kind: StepKind.Validation,
        description: "Blocks appraisal-assignment completion while a quotation child workflow is still active. Only active when the activity opts in via 'canRaiseQuotation'.");

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        var workflowInstance = await workflowInstanceRepository.GetByIdAsync(ctx.WorkflowInstanceId, ct);
        if (workflowInstance is null)
        {
            logger.LogWarning(
                "RequireNoActiveQuotationStep: workflow instance {WorkflowInstanceId} not found — skipping",
                ctx.WorkflowInstanceId);
            return ProcessStepResult.Pass();
        }

        if (!ActivityCanRaiseQuotation(workflowInstance.WorkflowDefinition?.JsonDefinition, ctx.ActivityId))
        {
            logger.LogDebug(
                "RequireNoActiveQuotationStep: activity {ActivityId} has not opted in — skipping",
                ctx.ActivityId);
            return ProcessStepResult.Pass();
        }

        // CorrelationId of the parent workflow = RequestId
        if (!Guid.TryParse(workflowInstance.CorrelationId, out var requestId))
        {
            logger.LogDebug(
                "RequireNoActiveQuotationStep: workflow {WorkflowInstanceId} has no parseable CorrelationId — skipping",
                ctx.WorkflowInstanceId);
            return ProcessStepResult.Pass();
        }

        try
        {
            using var connection = connectionFactory.GetOpenConnection();

            var activeCount = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM appraisal.QuotationRequests qr
                JOIN appraisal.QuotationRequestAppraisals qra ON qra.QuotationRequestId = qr.Id
                JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
                WHERE a.RequestId = @RequestId
                  AND qr.Status NOT IN ('Cancelled', 'Finalized')
                """,
                new { RequestId = requestId });

            if (activeCount > 0)
            {
                logger.LogWarning(
                    "RequireNoActiveQuotationStep: {Count} active quotation(s) found for RequestId={RequestId} — blocking completion of activity {ActivityId}",
                    activeCount, requestId, ctx.ActivityId);

                return ProcessStepResult.Fail(
                    "ACTIVE_QUOTATION_EXISTS",
                    "There is an active quotation process for this case. The quotation must be finalized or cancelled before this task can be completed.");
            }

            return ProcessStepResult.Pass();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "RequireNoActiveQuotationStep: failed to check active quotations for RequestId={RequestId}",
                requestId);
            return ProcessStepResult.Error(ex);
        }
    }

    /// <summary>
    /// Checks whether the given activity's JSON definition has <c>"canRaiseQuotation": true</c>.
    /// </summary>
    private static bool ActivityCanRaiseQuotation(string? jsonDefinition, string activityId)
    {
        if (string.IsNullOrEmpty(jsonDefinition)) return false;

        try
        {
            using var doc = JsonDocument.Parse(jsonDefinition);
            var root = doc.RootElement;
            if (root.TryGetProperty("workflowSchema", out var schema))
                root = schema;

            if (!root.TryGetProperty("activities", out var activities) ||
                activities.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var activity in activities.EnumerateArray())
            {
                if (!activity.TryGetProperty("id", out var idProp) || idProp.GetString() != activityId)
                    continue;

                if (!activity.TryGetProperty("properties", out var props) ||
                    props.ValueKind != JsonValueKind.Object)
                    return false;

                if (!props.TryGetProperty("canRaiseQuotation", out var flag))
                    return false;

                return flag.ValueKind == JsonValueKind.True ||
                       (flag.ValueKind == JsonValueKind.String &&
                        bool.TryParse(flag.GetString(), out var b) && b);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}

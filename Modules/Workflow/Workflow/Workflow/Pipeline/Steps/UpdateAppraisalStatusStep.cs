using System.Text.Json;
using Microsoft.Extensions.Logging;
using Appraisal.Contracts.Services;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Updates the appraisal aggregate's status using IAppraisalStatusService.
/// Reads "targetStatus" from the step's Parameters JSON.
/// </summary>
public class UpdateAppraisalStatusStep(
    IAppraisalStatusService appraisalStatusService,
    ILogger<UpdateAppraisalStatusStep> logger) : IActivityProcessStep
{
    public string Name => "UpdateAppraisalStatus";

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext context, CancellationToken ct)
    {
        var targetStatus = GetTargetStatus(context.Parameters);
        if (targetStatus is null)
            return ProcessStepResult.Fail("Missing 'targetStatus' in step parameters");

        try
        {
            await appraisalStatusService.UpdateStatusAsync(
                context.AppraisalId, targetStatus, context.CompletedBy, ct);

            logger.LogInformation(
                "Updated appraisal {AppraisalId} status to {TargetStatus}",
                context.AppraisalId, targetStatus);

            return ProcessStepResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to update appraisal {AppraisalId} status to {TargetStatus}",
                context.AppraisalId, targetStatus);
            return ProcessStepResult.Fail(ex.Message);
        }
    }

    private static string? GetTargetStatus(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(parameters);
            return doc.RootElement.TryGetProperty("targetStatus", out var prop)
                ? prop.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}

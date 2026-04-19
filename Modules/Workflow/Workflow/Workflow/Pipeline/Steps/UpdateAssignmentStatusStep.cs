using Microsoft.Extensions.Logging;
using Appraisal.Contracts.Services;
using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Updates the active assignment's status using IAppraisalStatusService.
/// </summary>
public class UpdateAssignmentStatusStep(
    IAppraisalStatusService appraisalStatusService,
    ILogger<UpdateAssignmentStatusStep> logger) : IActivityProcessStep
{
    public sealed record Parameters
    {
        /// <summary>Target assignment status to transition to (e.g., "Completed").</summary>
        public string? TargetStatus { get; init; }
    }

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "UpdateAssignmentStatus",
        displayName: "Update Assignment Status",
        kind: StepKind.Action,
        description: "Transitions the active appraisal assignment to the specified status.");

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        if (ctx.AppraisalId is null)
            return ProcessStepResult.Fail("APPRAISAL_NOT_CREATED", "Appraisal not yet created");

        var p = ctx.GetParameters<Parameters>();
        if (string.IsNullOrWhiteSpace(p.TargetStatus))
            return ProcessStepResult.Fail("MISSING_TARGET_STATUS", "Missing 'targetStatus' in step parameters");

        try
        {
            await appraisalStatusService.UpdateAssignmentStatusAsync(
                ctx.AppraisalId.Value, p.TargetStatus, ctx.CompletedBy, ct);

            logger.LogInformation(
                "Updated assignment for appraisal {AppraisalId} status to {TargetStatus}",
                ctx.AppraisalId, p.TargetStatus);

            return ProcessStepResult.Pass();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to update assignment for appraisal {AppraisalId} status to {TargetStatus}",
                ctx.AppraisalId, p.TargetStatus);
            return ProcessStepResult.Error(ex);
        }
    }
}

using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Validates that the appraisal has at least one property with an appraised value.
/// </summary>
public class ValidateHasAppraisedValueStep(
    ISqlConnectionFactory connectionFactory,
    ILogger<ValidateHasAppraisedValueStep> logger) : IActivityProcessStep
{
    public sealed record Parameters;

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "ValidateHasAppraisedValue",
        displayName: "Validate Has Appraised Value",
        kind: StepKind.Validation,
        description: "Ensures the appraisal has at least one property with an appraised value > 0.");

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        if (ctx.AppraisalId is null)
            return ProcessStepResult.Fail("APPRAISAL_NOT_CREATED", "Appraisal not yet created");

        try
        {
            using var connection = connectionFactory.GetOpenConnection();

            var hasValue = await connection.ExecuteScalarAsync<bool>(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM appraisal.ValuationAnalyses
                    WHERE AppraisalId = @AppraisalId
                      AND AppraisedValue > 0
                ) THEN 1 ELSE 0 END
                """,
                new { AppraisalId = ctx.AppraisalId.Value });

            if (!hasValue)
            {
                logger.LogWarning("Appraisal {AppraisalId} has no appraised value set", ctx.AppraisalId);
                return ProcessStepResult.Fail(
                    "NO_APPRAISED_VALUE",
                    "Appraised value is required before completing this activity");
            }

            return ProcessStepResult.Pass();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to validate appraised value for appraisal {AppraisalId}", ctx.AppraisalId);
            return ProcessStepResult.Error(ex);
        }
    }
}

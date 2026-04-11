using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Validates that the appraisal has at least one property with an appraised value.
/// Uses Dapper for a lightweight read query.
/// </summary>
public class ValidateHasAppraisedValueStep(
    ISqlConnectionFactory connectionFactory,
    ILogger<ValidateHasAppraisedValueStep> logger) : IActivityProcessStep
{
    public string Name => "ValidateHasAppraisedValue";

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext context, CancellationToken ct)
    {
        if (context.AppraisalId is null)
            return ProcessStepResult.Fail("Appraisal not yet created");

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
                new { AppraisalId = context.AppraisalId.Value });

            if (!hasValue)
            {
                logger.LogWarning(
                    "Appraisal {AppraisalId} has no appraised value set", context.AppraisalId);
                return ProcessStepResult.Fail("Appraised value is required before completing this activity");
            }

            return ProcessStepResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to validate appraised value for appraisal {AppraisalId}", context.AppraisalId);
            return ProcessStepResult.Fail(ex.Message);
        }
    }
}

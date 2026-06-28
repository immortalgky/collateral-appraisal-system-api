using Carter;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.OperationalReports.Shared;
using Shared.Data;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.PartySla;

/// <summary>
/// Surfaces vendor vs bank party-SLA measurement for a specific appraisal.
/// Uses <see cref="IPartySlaEvaluator"/> to measure business time across rework cycles
/// and compare against Stage-scope SLA budgets.
///
/// Authorization: login-only (same as other operational reports).
/// </summary>
public sealed class PartySlaEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/party-sla",
                async (
                    Guid appraisalId,
                    IPartySlaEvaluator evaluator,
                    ISqlConnectionFactory connectionFactory,
                    CancellationToken ct) =>
                {
                    // Resolve appraisal metadata needed for policy lookups.
                    var appraisal = (await connectionFactory.QueryAsync<AppraisalMeta>(
                        """
                        SELECT
                            a.RequestId          AS CorrelationId,
                            a.BankingSegment     AS LoanType,
                            a.AppraisalType      AS AppraisalType,
                            wi.WorkflowDefinitionId
                        FROM appraisal.Appraisals a
                        LEFT JOIN workflow.WorkflowInstances wi
                            ON wi.CorrelationId = CAST(a.RequestId AS nvarchar(36))
                        WHERE a.Id = @AppraisalId
                        """,
                        new DynamicParameters(new { AppraisalId = appraisalId })
                    )).FirstOrDefault();

                    if (appraisal is null)
                        return Results.NotFound(new { error = "Appraisal not found" });

                    if (appraisal.CorrelationId == Guid.Empty)
                        return Results.Ok(new { vendor = (object?)null, bank = (object?)null, message = "No workflow correlation found" });

                    var result = await evaluator.EvaluateAsync(
                        appraisal.CorrelationId,
                        appraisal.WorkflowDefinitionId,
                        companyId: null,       // stage budgets are company-agnostic (seeded without CompanyId)
                        appraisal.LoanType,
                        appraisal.AppraisalType,
                        ct);

                    if (result is null)
                        return Results.Ok(new { vendor = (object?)null, bank = (object?)null, message = "No completed tasks found" });

                    return Results.Ok(result);
                })
            .WithName("GetAppraisalPartySla")
            .Produces<PartySlaResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Party SLA evaluation")
            .WithDescription("Returns vendor and bank elapsed business-time cycles with OLA budget comparison for the given appraisal.")
            .WithTags("Appraisal", "OLA")
            .RequireAuthorization();
    }

    private sealed record AppraisalMeta(Guid CorrelationId, Guid? WorkflowDefinitionId, string? LoanType, string? AppraisalType);
}

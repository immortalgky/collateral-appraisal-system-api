namespace Appraisal.Application.Evaluations.Configs;

/// <summary>
/// Admin endpoints for managing EvaluationCriteriaConfig rows.
/// These allow administrators to configure evaluation criteria labels,
/// weights, guidance, and thresholds per banking segment without a deploy.
/// </summary>
public class EvaluationConfigEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ── GET configs by banking segment ─────────────────────────────────────
        app.MapGet(
                "/appraisal-evaluation-configs",
                async (
                    string bankingSegment,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetEvaluationConfigsQuery(bankingSegment);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetAppraisalEvaluationConfigs")
            .Produces<List<EvaluationConfigDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get evaluation criteria configs for a banking segment")
            .WithDescription("Returns the 5 criteria configuration rows (ordered by DisplayOrder) " +
                             "for the given bankingSegment ('Retail' or 'IBG'). " +
                             "The segment match is case-insensitive.")
            .WithTags("AppraisalEvaluationConfig")
            .RequireAuthorization();

        // ── PUT update config ──────────────────────────────────────────────────
        app.MapPut(
                "/appraisal-evaluation-configs/{id:guid}",
                async (
                    Guid id,
                    UpdateEvaluationConfigRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new UpdateEvaluationConfigCommand(
                        id,
                        request.LabelEn,
                        request.LabelTh,
                        request.Weight,
                        request.MaxScore,
                        request.GuidanceJson,
                        request.ThresholdsJson);

                    await sender.Send(command, cancellationToken);

                    return Results.Ok();
                })
            .WithName("UpdateAppraisalEvaluationConfig")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Update an evaluation criteria config row")
            .WithDescription("Updates mutable fields (LabelEn, LabelTh, Weight, MaxScore, GuidanceJson, ThresholdsJson). " +
                             "BankingSegment, CriteriaSlot, and CriteriaKey are immutable.")
            .WithTags("AppraisalEvaluationConfig")
            .RequireAuthorization();
    }
}

// ── Request DTO ───────────────────────────────────────────────────────────────

public record UpdateEvaluationConfigRequest(
    string  LabelEn,
    string  LabelTh,
    decimal Weight,
    int     MaxScore,
    string  GuidanceJson,
    string? ThresholdsJson);

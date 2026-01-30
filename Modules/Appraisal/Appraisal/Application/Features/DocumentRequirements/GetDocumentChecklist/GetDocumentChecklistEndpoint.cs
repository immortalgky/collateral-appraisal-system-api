namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentChecklist;

/// <summary>
/// Endpoint: GET /document-checklist?collateralTypeCodes=L,B
/// Returns document checklist with application-level and collateral-specific documents
/// </summary>
public class GetDocumentChecklistEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/document-checklist", async (
                [FromQuery] string collateralTypeCodes,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(collateralTypeCodes))
                {
                    return Results.BadRequest(new { error = "collateralTypeCodes parameter is required" });
                }

                var codes = collateralTypeCodes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                if (codes.Count == 0)
                {
                    return Results.BadRequest(new { error = "At least one collateral type code is required" });
                }

                var query = new GetDocumentChecklistQuery(codes);
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(new GetDocumentChecklistResponse(
                    result.ApplicationDocuments,
                    result.CollateralGroups));
            })
            .WithName("GetDocumentChecklist")
            .WithSummary("Get document checklist for specified collateral types")
            .WithDescription("Returns application-level documents and collateral-specific documents grouped by type")
            .Produces<GetDocumentChecklistResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Document Requirements");
    }
}

/// <summary>
/// API Response for document checklist
/// </summary>
public record GetDocumentChecklistResponse(
    IReadOnlyList<DocumentChecklistItemDto> ApplicationDocuments,
    IReadOnlyList<CollateralDocumentGroupDto> CollateralGroups);

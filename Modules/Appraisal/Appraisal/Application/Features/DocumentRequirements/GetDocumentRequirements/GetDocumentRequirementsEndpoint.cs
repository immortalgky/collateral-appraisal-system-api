namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentRequirements;

/// <summary>
/// Endpoint: GET /document-requirements
/// Returns all document requirements for admin management
/// </summary>
public class GetDocumentRequirementsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/document-requirements", async (
                [FromQuery] string? collateralTypeCode,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetDocumentRequirementsQuery(collateralTypeCode);
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(result.Requirements);
            })
            .WithName("GetDocumentRequirements")
            .WithSummary("Get all document requirements")
            .WithDescription("Returns all document requirements. Use collateralTypeCode=APP for application-level, or L/B/U/VEH/etc. for collateral-specific")
            .Produces<IReadOnlyList<DocumentRequirementDto>>()
            .WithTags("Document Requirements");
    }
}

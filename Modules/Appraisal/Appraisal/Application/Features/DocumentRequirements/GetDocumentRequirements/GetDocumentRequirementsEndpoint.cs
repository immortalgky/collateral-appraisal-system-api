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
                [FromQuery] string? propertyTypeCode,
                [FromQuery] string? purposeCode,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetDocumentRequirementsQuery(propertyTypeCode, purposeCode);
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(new GetDocumentRequirementsResponse(result.Requirements));
            })
            .WithName("GetDocumentRequirements")
            .WithSummary("Get all document requirements")
            .WithDescription("Returns all document requirements. Use propertyTypeCode=APP for application-level, or L/B/U/VEH/etc. for property-type-specific. Use purposeCode to filter by purpose.")
            .Produces<GetDocumentRequirementsResponse>()
            .WithTags("Document Requirements");
    }
}

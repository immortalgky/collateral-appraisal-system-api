namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentChecklist;

/// <summary>
/// Endpoint: GET /document-checklist?propertyTypeCodes=L,B&amp;purposeCode=01
/// Returns document checklist with application-level and property-type-specific documents
/// </summary>
public class GetDocumentChecklistEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/document-checklist", async (
                [FromQuery] string propertyTypeCodes,
                [FromQuery] string? purposeCode,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(propertyTypeCodes))
                {
                    return Results.BadRequest(new { error = "propertyTypeCodes parameter is required" });
                }

                var codes = propertyTypeCodes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                if (codes.Count == 0)
                {
                    return Results.BadRequest(new { error = "At least one property type code is required" });
                }

                var query = new GetDocumentChecklistQuery(codes, purposeCode);
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(new GetDocumentChecklistResponse(
                    result.ApplicationDocuments,
                    result.PropertyTypeGroups));
            })
            .WithName("GetDocumentChecklist")
            .WithSummary("Get document checklist for specified property types")
            .WithDescription("Returns application-level documents and property-type-specific documents grouped by type. Use purposeCode to include purpose-specific requirements.")
            .Produces<GetDocumentChecklistResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Document Requirements");
    }
}

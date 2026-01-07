namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentTypes;

/// <summary>
/// Endpoint: GET /document-types
/// Returns all document types for admin management
/// </summary>
public class GetDocumentTypesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/document-types", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new GetDocumentTypesQuery();
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(result.DocumentTypes);
            })
            .WithName("GetDocumentTypes")
            .WithSummary("Get all document types")
            .WithDescription("Returns all document types for admin configuration")
            .Produces<IReadOnlyList<DocumentTypeDto>>()
            .WithTags("Document Types");
    }
}

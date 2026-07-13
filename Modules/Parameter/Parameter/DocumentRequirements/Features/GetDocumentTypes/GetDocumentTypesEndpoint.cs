namespace Parameter.DocumentRequirements.Features.GetDocumentTypes;

public class GetDocumentTypesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/document-types", async (
                [FromQuery] bool? includeInactive,
                [FromQuery] string? category,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetDocumentTypesQuery(includeInactive ?? false, category);
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(new GetDocumentTypesResponse(result.DocumentTypes));
            })
            .RequireAuthorization()
            .WithName("GetDocumentTypes")
            .WithSummary("Get all document types")
            .Produces<GetDocumentTypesResponse>()
            .WithTags("Document Types");
    }
}

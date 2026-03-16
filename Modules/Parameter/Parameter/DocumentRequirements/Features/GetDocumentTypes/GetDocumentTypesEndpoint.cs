namespace Parameter.DocumentRequirements.Features.GetDocumentTypes;

public class GetDocumentTypesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/document-types", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new GetDocumentTypesQuery();
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(new GetDocumentTypesResponse(result.DocumentTypes));
            })
            .WithName("GetDocumentTypes")
            .WithSummary("Get all document types")
            .Produces<GetDocumentTypesResponse>()
            .WithTags("Document Types");
    }
}

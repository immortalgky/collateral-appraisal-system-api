namespace Document.Documents.Features.GetDocuments;

public class GetDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/documents", async (ISender sender) =>
        {
            var query = new GetDocumentQuery();

            var result = await sender.Send(query);

            var response = result.Adapt<GetDocumentResponse>();

            return Results.Ok(response.Documents);
        });
    }
}
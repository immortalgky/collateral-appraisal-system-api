namespace Document.Documents.Features.GetDocumentById;

public class GetDocumentByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/documents/{id:long}", async (long id, ISender sender) =>
        {
            var query = new GetDocumentByIdQuery(id);

            var result = await sender.Send(query);

            var response = result.Adapt<GetDocumentByIdResponse>();

            return Results.Ok(response.Document);
        });
    }
}
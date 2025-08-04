namespace Document.Documents.Features.UpdateDocument;

public class UpdateDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/documents/{id:long}", async (UpdateDocumentRequest request, long id, ISender sender) =>
        {
            var command = request.Adapt<UpdateDocumentCommand>() with {Id = id};

            var result = await sender.Send(command);

            var response = result.Adapt<UpdateDocumentResponse>();

            return Results.Ok(response);
        });
    }
}
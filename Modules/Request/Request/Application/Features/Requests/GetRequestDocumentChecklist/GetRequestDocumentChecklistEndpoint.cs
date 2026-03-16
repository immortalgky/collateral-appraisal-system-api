namespace Request.Application.Features.Requests.GetRequestDocumentChecklist;

public class GetRequestDocumentChecklistEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{id:guid}/document-checklist", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetRequestDocumentChecklistQuery(id);
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(new GetRequestDocumentChecklistResponse(
                    result.ApplicationDocuments,
                    result.TitleDocuments,
                    result.IsComplete,
                    result.MissingRequiredCount));
            })
            .WithName("GetRequestDocumentChecklist")
            .WithSummary("Get document checklist for a specific request")
            .WithDescription("Returns the document checklist with upload status for application-level and per-title documents")
            .Produces<GetRequestDocumentChecklistResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Requests");
    }
}

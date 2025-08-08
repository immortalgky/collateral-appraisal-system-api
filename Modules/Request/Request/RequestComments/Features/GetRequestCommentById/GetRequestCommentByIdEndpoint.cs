namespace Request.RequestComments.Features.GetRequestCommentById;

public class GetRequestCommentByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:long}/comments/{commentId:long}",
            async (long requestId, long commentId, ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new GetRequestCommentByIdQuery(requestId, commentId);

                var result = await sender.Send(query, cancellationToken);

                var response = result.Adapt<GetRequestCommentByIdResponse>();

                return Results.Ok(response);
            })
            .WithName("GetRequestCommentById")
            .Produces<GetRequestCommentByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get request comment by ID")
            .WithDescription("Retrieves a specific comment for the specified request. Returns detailed information about the comment including creation and modification timestamps.")
            .WithTags("Request Comments");
    }
}
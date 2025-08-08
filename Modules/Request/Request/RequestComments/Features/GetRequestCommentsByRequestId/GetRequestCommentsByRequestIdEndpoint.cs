namespace Request.RequestComments.Features.GetRequestCommentsByRequestId;

public class GetRequestCommentsByRequestIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:long}/comments",
            async (long requestId, ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new GetRequestCommentsByRequestIdQuery(requestId);

                var result = await sender.Send(query, cancellationToken);

                var response = result.Adapt<GetRequestCommentsByRequestIdResponse>();

                return Results.Ok(response);
            })
            .WithName("GetRequestCommentsByRequestId")
            .Produces<GetRequestCommentsByRequestIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all comments for a request")
            .WithDescription("Retrieves all comments associated with the specified request. Comments are returned in chronological order (oldest first).")
            .WithTags("Request Comments");
    }
}
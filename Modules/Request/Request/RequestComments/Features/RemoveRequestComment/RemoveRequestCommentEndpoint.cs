namespace Request.RequestComments.Features.RemoveRequestComment;

public class RemoveRequestCommentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/requests/{requestId:long}/comments/{commentId:long}",
            async (long requestId, long commentId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RemoveRequestCommentCommand(commentId), cancellationToken);

                var response = result.Adapt<RemoveRequestCommentResponse>();

                return Results.Ok(response);
            })
            .WithName("RemoveRequestComment")
            .Produces<RemoveRequestCommentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a comment from a request")
            .WithDescription("Removes an existing comment from the specified request. This action cannot be undone.")
            .WithTags("Request Comments");
    }
}
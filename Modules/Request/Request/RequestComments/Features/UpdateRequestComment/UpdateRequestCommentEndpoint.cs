namespace Request.RequestComments.Features.UpdateRequestComment;

public class UpdateRequestCommentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{requestId:long}/comments/{commentId:long}", async (
            long requestId,
            long commentId,
            UpdateRequestCommentRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateRequestCommentCommand(commentId, request.Comment);

            var result = await sender.Send(command, cancellationToken);

            var response = result.Adapt<UpdateRequestCommentResponse>();

            return Results.Ok(response);
        })
        .WithName("UpdateRequestComment")
        .Produces<UpdateRequestCommentResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update a request comment")
        .WithDescription("Updates an existing comment on the specified request. Only the comment text can be modified.")
        .WithTags("Request Comments");
    }
}
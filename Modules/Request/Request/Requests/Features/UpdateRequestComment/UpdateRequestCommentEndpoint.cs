namespace Request.Requests.Features.UpdateRequestComment;

public class UpdateRequestCommentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/requests/{id:long}/comments/{commentId:long}", async (
            long id,
            long commentId,
            UpdateRequestCommentRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateRequestCommentCommand(id, commentId, request.Comment);

            var result = await sender.Send(command, cancellationToken);

            var response = result.Adapt<UpdateRequestCommentResponse>();

            return Results.Ok(response);
        });
    }
}
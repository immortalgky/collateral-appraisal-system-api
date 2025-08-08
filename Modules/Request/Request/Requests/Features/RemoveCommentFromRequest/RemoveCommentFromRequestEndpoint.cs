namespace Request.Requests.Features.RemoveCommentFromRequest;

public class RemoveCommentFromRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/requests/{id:long}/comments/{commentId:long}",
            async (long id, long commentId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RemoveCommentFromRequestCommand(id, commentId), cancellationToken);

                var response = result.Adapt<RemoveCommentFromRequestResponse>();

                return Results.Ok(response);
            });
    }
}
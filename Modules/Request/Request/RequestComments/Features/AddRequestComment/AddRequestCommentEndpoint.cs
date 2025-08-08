namespace Request.RequestComments.Features.AddRequestComment;

public class AddRequestCommentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:long}/comments",
            async (long requestId, AddRequestCommentRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new AddRequestCommentCommand(requestId, request.Comment);

                var result = await sender.Send(command, cancellationToken);

                var response = result.Adapt<AddRequestCommentResponse>();

                return Results.Ok(response);
            })
            .WithName("AddRequestComment")
            .Produces<AddRequestCommentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add a comment to a request")
            .WithDescription("Adds a new comment to the specified request. Comments are used to track communication and notes related to the appraisal request.")
            .WithTags("Request Comments");
    }
}
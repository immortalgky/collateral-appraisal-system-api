namespace Request.Requests.Features.AddCommentToRequest;

public class AddCommentToRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{id:long}/comments",
            async (long id, AddCommentToRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = request.Adapt<AddCommentToRequestCommand>() with { Id = id };

                var result = await sender.Send(command, cancellationToken);

                var response = result.Adapt<AddCommentToRequestResponse>();

                return Results.Ok(response);
            });
    }
}
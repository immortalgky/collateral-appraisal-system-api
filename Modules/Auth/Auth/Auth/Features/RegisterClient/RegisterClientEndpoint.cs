namespace Auth.Auth.Features.RegisterClient;

public class RegisterClientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/clients",
                async (
                    RegisterClientRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<RegisterClientCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<RegisterClientResponse>();

                    return Results.Ok(response);
                }
            )
            .AllowAnonymous()
            .WithName("RegisterClient")
            .Produces<RegisterClientResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Register new client")
            .WithDescription("Register a new client.")
            .WithTags("Auth");
    }
}

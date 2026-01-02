namespace Auth.Domain.Auth.Features.RegisterUser;

public class RegisterUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/register",
                async (
                    RegisterUserRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<RegisterUserCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<RegisterUserResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("RegisterUser")
            .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Register new user account")
            .WithDescription("Register a new user account.")
            .WithTags("Auth")
            .AllowAnonymous();
    }
}

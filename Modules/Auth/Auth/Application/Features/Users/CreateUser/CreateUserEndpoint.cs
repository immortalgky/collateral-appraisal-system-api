namespace Auth.Application.Features.Users.CreateUser;

public class CreateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/users",
                async (CreateUserRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<CreateUserCommand>();
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<CreateUserResponse>();
                    return Results.Created($"/auth/users/{response.Id}", response);
                })
            .WithName("CreateUser")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create user (admin)")
            .WithDescription("Admin creation of a new user account with roles.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}

namespace Auth.Application.Features.Users.SetUserActivation;

public class SetUserActivationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/users/{id:guid}/activation",
                async (Guid id, SetUserActivationRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new SetUserActivationCommand(id, request.IsActive);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("SetUserActivation")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Activate or deactivate a user")
            .WithDescription("Set the active state of a user account.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}

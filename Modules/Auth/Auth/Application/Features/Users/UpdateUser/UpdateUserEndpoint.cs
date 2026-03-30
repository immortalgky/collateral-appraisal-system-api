namespace Auth.Application.Features.Users.UpdateUser;

public class UpdateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/users/{id:guid}",
                async (Guid id, UpdateUserRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateUserCommand(
                        id,
                        request.FirstName,
                        request.LastName,
                        request.Position,
                        request.Department,
                        request.CompanyId);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateUser")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update user (admin)")
            .WithDescription("Admin update of user profile fields.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}

namespace Auth.Application.Features.Users.UpdateUserRoles;

public class UpdateUserRolesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/users/{id:guid}/roles",
                async (Guid id, UpdateUserRolesRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateUserRolesCommand(id, request.RoleNames);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateUserRoles")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update user roles (admin)")
            .WithDescription("Replace all role assignments for a user.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}

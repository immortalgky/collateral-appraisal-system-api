namespace Auth.Application.Features.Users.UpdateUserGroups;

public class UpdateUserGroupsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/users/{id:guid}/groups",
                async (Guid id, UpdateUserGroupsRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateUserGroupsCommand(id, request.GroupIds);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateUserGroups")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update user groups (admin)")
            .WithDescription("Replace all group memberships for a user.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}

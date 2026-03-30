namespace Auth.Application.Features.Groups.UpdateGroupUsers;

public class UpdateGroupUsersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/groups/{id:guid}/users",
                async (Guid id, UpdateGroupUsersRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateGroupUsersCommand(id, request.UserIds);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateGroupUsers")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update group users")
            .WithDescription("Replace all users in a group.")
            .WithTags("Group")
            .RequireAuthorization("CanManageGroups");
    }
}

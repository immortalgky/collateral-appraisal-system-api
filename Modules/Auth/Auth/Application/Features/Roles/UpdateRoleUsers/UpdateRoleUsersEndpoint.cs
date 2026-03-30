namespace Auth.Application.Features.Roles.UpdateRoleUsers;

public class UpdateRoleUsersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/roles/{id:guid}/users",
                async (Guid id, UpdateRoleUsersRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateRoleUsersCommand(id, request.UserIds);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<UpdateRoleUsersResponse>();
                    return Results.Ok(response);
                })
            .WithName("UpdateRoleUsers")
            .Produces<UpdateRoleUsersResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Replace users in a role")
            .WithDescription("Replace the full set of users assigned to a role.")
            .WithTags("Role")
            .RequireAuthorization("CanManageRoles");
    }
}

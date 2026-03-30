namespace Auth.Application.Features.Roles.UpdateRolePermissions;

public class UpdateRolePermissionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/roles/{id:guid}/permissions",
                async (Guid id, UpdateRolePermissionsRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateRolePermissionsCommand(id, request.PermissionIds);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<UpdateRolePermissionsResponse>();
                    return Results.Ok(response);
                })
            .WithName("UpdateRolePermissions")
            .Produces<UpdateRolePermissionsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Replace role permissions")
            .WithDescription("Replace the full set of permissions assigned to a role.")
            .WithTags("Role")
            .RequireAuthorization("CanManageRoles");
    }
}

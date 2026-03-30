namespace Auth.Application.Features.Roles.DeleteRole;

public class DeleteRoleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/auth/roles/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new DeleteRoleCommand(id), cancellationToken);
                    var response = result.Adapt<DeleteRoleResponse>();
                    return Results.Ok(response);
                })
            .WithName("DeleteRole")
            .Produces<DeleteRoleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete role by ID")
            .WithDescription("Deletes a role by its ID.")
            .WithTags("Role")
            .RequireAuthorization("CanManageRoles");
    }
}

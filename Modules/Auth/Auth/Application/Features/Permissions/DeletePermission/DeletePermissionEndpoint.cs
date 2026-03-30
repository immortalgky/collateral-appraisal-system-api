namespace Auth.Application.Features.Permissions.DeletePermission;

public class DeletePermissionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/auth/permissions/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new DeletePermissionCommand(id), cancellationToken);
                    var response = result.Adapt<DeletePermissionResponse>();
                    return Results.Ok(response);
                })
            .WithName("DeletePermission")
            .Produces<DeletePermissionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete permission by ID")
            .WithDescription("Deletes a permission by its ID.")
            .WithTags("Permission")
            .RequireAuthorization("CanManagePermissions");
    }
}

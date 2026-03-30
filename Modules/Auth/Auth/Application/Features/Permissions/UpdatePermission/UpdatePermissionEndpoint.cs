namespace Auth.Application.Features.Permissions.UpdatePermission;

public class UpdatePermissionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/permissions/{id:guid}",
                async (Guid id, UpdatePermissionRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdatePermissionCommand(id, request.DisplayName, request.Description, request.Module);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<UpdatePermissionResponse>();
                    return Results.Ok(response);
                })
            .WithName("UpdatePermission")
            .Produces<UpdatePermissionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a permission")
            .WithDescription("Update an existing permission by its ID.")
            .WithTags("Permission")
            .RequireAuthorization("CanManagePermissions");
    }
}

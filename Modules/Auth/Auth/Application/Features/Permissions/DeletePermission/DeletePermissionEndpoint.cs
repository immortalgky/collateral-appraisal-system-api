namespace Auth.Domain.Permissions.Features.DeletePermission;

public class DeletePermissionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/auth/permissions/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new DeletePermissionCommand(id);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<DeletePermissionResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("DeletePermission")
            .Produces<DeletePermissionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete permission by ID")
            .WithDescription(
                "Deletes a permission by its ID. If the permission does not exist, a 404 Not Found error is returned."
            )
            .WithTags("Permission");
    }
}

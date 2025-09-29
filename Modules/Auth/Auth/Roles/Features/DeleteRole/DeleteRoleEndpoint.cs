namespace Auth.Roles.Features.DeleteRole;

public class DeleteRoleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/auth/roles/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new DeleteRoleCommand(id);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<DeleteRoleResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("DeleteRole")
            .Produces<DeleteRoleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete role by ID")
            .WithDescription(
                "Deletes a role by its ID. If the role does not exist, a 404 Not Found error is returned."
            )
            .WithTags("Role");
    }
}

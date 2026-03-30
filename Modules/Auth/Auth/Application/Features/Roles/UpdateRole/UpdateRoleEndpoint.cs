namespace Auth.Application.Features.Roles.UpdateRole;

public class UpdateRoleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/roles/{id:guid}",
                async (Guid id, UpdateRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateRoleCommand(id, request.Name, request.Description, request.Scope);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<UpdateRoleResponse>();
                    return Results.Ok(response);
                })
            .WithName("UpdateRole")
            .Produces<UpdateRoleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a role")
            .WithDescription("Update the name, description, and scope of an existing role.")
            .WithTags("Role")
            .RequireAuthorization("CanManageRoles");
    }
}

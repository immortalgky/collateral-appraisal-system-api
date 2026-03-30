namespace Auth.Application.Features.Roles.CreateRole;

public class CreateRoleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/roles",
                async (CreateRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<CreateRoleCommand>();
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<CreateRoleResponse>();
                    return Results.Created($"/auth/roles/{response.Id}", response);
                })
            .WithName("CreateRole")
            .Produces<CreateRoleResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new role")
            .WithDescription("Create a new role with optional permissions and scope (Bank/Company).")
            .WithTags("Role")
            .RequireAuthorization("CanManageRoles");
    }
}

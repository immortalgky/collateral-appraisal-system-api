namespace Auth.Domain.Roles.Features.CreateRole;

public class CreateRoleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/roles",
                async (
                    CreateRoleRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateRoleCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateRoleResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateRole")
            .Produces<CreateRoleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create new role")
            .WithDescription("Create new role.")
            .WithTags("Role");
    }
}

namespace Auth.Application.Features.Roles.GetRoles;

public class GetRoleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/roles",
                async (
                    [AsParameters] GetRoleQuery query,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetRoleResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetRoles")
            .Produces<GetRoleResponse>()
            .WithSummary("Get all roles")
            .WithDescription("Retrieves all roles with optional search and scope filter, paginated.")
            .WithTags("Role");
    }
}

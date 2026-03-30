namespace Auth.Application.Features.Roles.GetRoleUsers;

public class GetRoleUsersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/roles/{id:guid}/users",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetRoleUsersQuery(id), cancellationToken);
                    var response = result.Adapt<GetRoleUsersResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetRoleUsers")
            .Produces<GetRoleUsersResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get users in a role")
            .WithDescription("Returns all users currently assigned to the specified role.")
            .WithTags("Role");
    }
}

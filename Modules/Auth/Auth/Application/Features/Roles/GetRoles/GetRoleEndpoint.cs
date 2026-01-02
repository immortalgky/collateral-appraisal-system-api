namespace Auth.Domain.Roles.Features.GetRoles;

public class GetRoleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/roles",
                async (
                    [AsParameters] PaginationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetRoleQuery(request);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetRoleResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetRoles")
            .Produces<GetRoleResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all roles")
            .WithDescription(
                "Retrieves all roles from the system. This endpoint returns a list of roles with their details."
            )
            .WithTags("Role");
    }
}

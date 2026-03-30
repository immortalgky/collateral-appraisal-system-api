namespace Auth.Application.Features.Roles.GetRoleById;

public class GetRoleByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/roles/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetRoleByIdQuery(id), cancellationToken);
                    var response = result.Adapt<GetRoleByIdResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetRoleById")
            .Produces<GetRoleByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get role by ID")
            .WithDescription("Get a role by its unique identifier, including permissions.")
            .WithTags("Role");
    }
}

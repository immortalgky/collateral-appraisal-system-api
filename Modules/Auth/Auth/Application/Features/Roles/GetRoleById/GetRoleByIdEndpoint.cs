namespace Auth.Domain.Roles.Features.GetRoleById;

public class GetRoleByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/roles/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetRoleByIdQuery(id);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetRoleByIdResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetRoleById")
            .Produces<GetRoleByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get role by ID")
            .WithDescription("Get role by ID")
            .WithTags("Role");
    }
}

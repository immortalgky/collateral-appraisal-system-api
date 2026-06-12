namespace Auth.Application.Features.Users.GetUserById;

public class GetUserByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/users/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetUserByIdQuery(id);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetUserById")
            .Produces<GetUserByIdResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get user by ID (admin)")
            .WithDescription("Get full user detail including roles.")
            .WithTags("User");
    }
}

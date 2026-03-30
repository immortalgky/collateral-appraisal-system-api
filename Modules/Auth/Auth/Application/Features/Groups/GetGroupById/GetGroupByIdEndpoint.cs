namespace Auth.Application.Features.Groups.GetGroupById;

public class GetGroupByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/groups/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetGroupByIdQuery(id);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetGroupById")
            .Produces<GetGroupByIdResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get group by ID")
            .WithDescription("Get group detail including users and monitored groups.")
            .WithTags("Group");
    }
}

namespace Auth.Permissions.Features.GetPermissionById;

public class GetPermissionByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/permissions/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetPermissionByIdQuery(id);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetPermissionByIdResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetPermissionById")
            .Produces<GetPermissionByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get permission by ID")
            .WithDescription("Get permission by ID")
            .WithTags("Permission");
    }
}

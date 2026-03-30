namespace Auth.Application.Features.Permissions.GetPermissionById;

public class GetPermissionByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/permissions/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetPermissionByIdQuery(id), cancellationToken);
                    var response = result.Adapt<GetPermissionByIdResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetPermissionById")
            .Produces<GetPermissionByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get permission by ID")
            .WithDescription("Get a permission by its unique identifier.")
            .WithTags("Permission");
    }
}

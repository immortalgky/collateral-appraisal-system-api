namespace Auth.Permissions.Features.GetPermissions;

public class GetPermissionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/permissions",
                async (
                    [AsParameters] PaginationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetPermissionQuery(request);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetPermissionResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetPermissions")
            .Produces<GetPermissionResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all permissions")
            .WithDescription(
                "Retrieves all permissions from the system. This endpoint returns a list of permissions with their details."
            )
            .WithTags("Permission");
    }
}

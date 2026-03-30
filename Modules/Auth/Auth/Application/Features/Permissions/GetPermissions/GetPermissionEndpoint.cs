namespace Auth.Application.Features.Permissions.GetPermissions;

public class GetPermissionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/permissions",
                async (
                    [AsParameters] GetPermissionQuery query,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetPermissionResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetPermissions")
            .Produces<GetPermissionResponse>()
            .WithSummary("Get all permissions")
            .WithDescription("Retrieves all permissions with optional search, paginated.")
            .WithTags("Permission");
    }
}

namespace Auth.Application.Features.Permissions.CreatePermission;

public class CreatePermissionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/permissions",
                async (
                    CreatePermissionRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<CreatePermissionCommand>();
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<CreatePermissionResponse>();
                    return Results.Created($"/auth/permissions/{response.Id}", response);
                })
            .WithName("CreatePermission")
            .Produces<CreatePermissionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new permission")
            .WithDescription("Create a new permission.")
            .WithTags("Permission")
            .RequireAuthorization("CanManagePermissions");
    }
}

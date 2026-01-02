namespace Auth.Domain.Permissions.Features.CreatePermission;

public class CreatePermissionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/permissions",
                async (
                    CreatePermissionRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreatePermissionCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreatePermissionResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreatePermission")
            .Produces<CreatePermissionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create new permission")
            .WithDescription("Create new permission.")
            .WithTags("Permission");
    }
}

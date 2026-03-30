namespace Auth.Application.Features.Groups.UpdateGroup;

public class UpdateGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/groups/{id:guid}",
                async (Guid id, UpdateGroupRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateGroupCommand(id, request.Name, request.Description);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateGroup")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a group")
            .WithDescription("Update the name and description of a group.")
            .WithTags("Group")
            .RequireAuthorization("CanManageGroups");
    }
}

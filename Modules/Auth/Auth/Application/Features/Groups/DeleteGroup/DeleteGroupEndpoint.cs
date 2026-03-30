using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Auth.Application.Features.Groups.DeleteGroup;

public class DeleteGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/auth/groups/{id:guid}",
                async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
                {
                    var deletedBy = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value is { } idStr
                        && Guid.TryParse(idStr, out var userId) ? userId : (Guid?)null;

                    var command = new DeleteGroupCommand(id, deletedBy);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("DeleteGroup")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a group")
            .WithDescription("Soft-delete a group.")
            .WithTags("Group")
            .RequireAuthorization("CanManageGroups");
    }
}

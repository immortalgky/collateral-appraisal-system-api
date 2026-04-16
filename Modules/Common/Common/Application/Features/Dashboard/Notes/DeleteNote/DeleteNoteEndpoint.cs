using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.Notes.DeleteNote;

public class DeleteNoteEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/dashboard/notes/{id:guid}",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var deleted = await sender.Send(new DeleteNoteCommand(id), cancellationToken);

                    // Handler returns false when note is not found or not owned by current user.
                    // Responding 404 in both cases — not 403 — to avoid leaking ownership information.
                    return deleted ? Results.NoContent() : Results.NotFound();
                })
            .WithName("DeleteNote")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a personal dashboard note; 404 if not found or not owned by current user")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}

using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.Notes.UpdateNote;

public class UpdateNoteEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/dashboard/notes/{id:guid}",
                async (
                    Guid id,
                    UpdateNoteRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new UpdateNoteCommand(id, request.Content);
                    var result = await sender.Send(command, cancellationToken);

                    // Handler returns null when note is not found or not owned by current user.
                    // Responding 404 in both cases — not 403 — to avoid leaking ownership information.
                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
            .WithName("UpdateNote")
            .Produces<NoteDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Update the content of a personal dashboard note; 404 if not found or not owned by current user")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}

using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.Notes.CreateNote;

public class CreateNoteEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/dashboard/notes",
                async (
                    CreateNoteRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new CreateNoteCommand(request.Content);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Created($"/dashboard/notes/{result.Id}", result);
                })
            .WithName("CreateNote")
            .Produces<NoteDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new personal dashboard note for the current user")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}

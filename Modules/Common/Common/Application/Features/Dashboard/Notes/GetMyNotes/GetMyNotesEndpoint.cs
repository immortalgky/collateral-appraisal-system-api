using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.Notes.GetMyNotes;

public class GetMyNotesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/notes",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetMyNotesQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetMyNotes")
            .Produces<GetMyNotesResponse>()
            .WithSummary("Get all personal dashboard notes for the current user, newest first")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}

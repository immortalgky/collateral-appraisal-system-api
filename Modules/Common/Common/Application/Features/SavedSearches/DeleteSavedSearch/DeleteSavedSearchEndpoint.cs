using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.SavedSearches.DeleteSavedSearch;

public class DeleteSavedSearchEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/saved-searches/{id:guid}",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var deleted = await sender.Send(new DeleteSavedSearchCommand(id), cancellationToken);

                    // Handler returns false when the search is not found or not owned by the current user.
                    // Responding 404 in both cases — not 403 — to avoid leaking ownership information.
                    return deleted ? Results.NoContent() : Results.NotFound();
                })
            .WithName("DeleteSavedSearch")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a saved search; 404 if not found or not owned by the current user")
            .WithTags("Saved Searches")
            .RequireAuthorization();
    }
}

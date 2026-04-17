using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.SavedSearches.GetSavedSearches;

public class GetSavedSearchesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/saved-searches",
                async (
                    string? entityType,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetSavedSearchesQuery(entityType), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetSavedSearches")
            .Produces<GetSavedSearchesResponse>()
            .WithSummary("List saved search configurations for the current user, optionally filtered by entity type")
            .WithTags("Saved Searches")
            .RequireAuthorization();
    }
}

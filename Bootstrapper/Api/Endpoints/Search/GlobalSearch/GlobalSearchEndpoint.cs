using Carter;
using Common.Application.Features.Search.GlobalSearch;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Api.Endpoints.Search.GlobalSearch;

public class GlobalSearchEndpoint : ICarterModule
{
    private static readonly HashSet<string> ValidFilters = ["all", "requests", "customers", "properties"];

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/search",
                async (string? q, string? filter, int? limit, ISender sender, CancellationToken cancellationToken) =>
                {
                    var searchTerm = q?.Trim() ?? "";
                    if (searchTerm.Length < 2)
                        return Results.BadRequest(new { detail = "Search query must be at least 2 characters." });

                    var filterValue = (filter ?? "all").ToLowerInvariant();
                    if (!ValidFilters.Contains(filterValue))
                        return Results.BadRequest(new { detail = $"Invalid filter. Allowed values: {string.Join(", ", ValidFilters)}" });

                    var limitValue = Math.Clamp(limit ?? 5, 1, 20);

                    var query = new GlobalSearchQuery(searchTerm, filterValue, limitValue);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GlobalSearch")
            .Produces<GlobalSearchResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Search")
            .WithSummary("Global search across requests, customers, and properties")
            .WithDescription("Unified search endpoint for the global search bar.")
            .AllowAnonymous();
    }
}

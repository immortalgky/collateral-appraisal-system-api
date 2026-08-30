using Appraisal.Application.Features.Appraisals.Shared;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.Appraisals.QuickSearch;

/// <summary>
/// The navbar quick-search. Kept on /search so the client's base path does not move.
/// </summary>
public class QuickSearchEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/search",
                async (string? q, string? scope, int? limit, ISender sender, CancellationToken cancellationToken) =>
                {
                    var term = q?.Trim() ?? "";
                    if (term.Length < AppraisalSearchPredicate.MinTermLength)
                        return Results.Problem(
                            title: "Search term too short",
                            detail: $"Search query must be at least {AppraisalSearchPredicate.MinTermLength} characters.",
                            statusCode: StatusCodes.Status400BadRequest);

                    var scopeValue = (scope ?? "all").ToLowerInvariant();
                    if (!AppraisalSearchPredicate.Scopes.Contains(scopeValue))
                        return Results.Problem(
                            title: "Invalid scope",
                            detail: $"Allowed values: {string.Join(", ", AppraisalSearchPredicate.Scopes)}.",
                            statusCode: StatusCodes.Status400BadRequest);

                    var result = await sender.Send(
                        new QuickSearchQuery(term, scopeValue, limit ?? 8), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("QuickSearch")
            .Produces<QuickSearchResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Search")
            .WithSummary("Search appraisals by number, customer or collateral")
            .WithDescription(
                "Every result is an appraisal. `scope` selects which group of columns is searched " +
                "(all | documents | customers | properties), not what kind of entity is returned. " +
                "Terms match by prefix; a leading `*` opts into substring matching.")
            // Was AllowAnonymous, which served customer names, phone numbers and title deeds to
            // anyone who could reach the host. Results are additionally scoped to the caller's
            // company for external valuation firms.
            .RequireAuthorization();
    }
}

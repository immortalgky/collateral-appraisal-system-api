using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.SavedSearches.CreateSavedSearch;

public class CreateSavedSearchEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/saved-searches",
                async (
                    CreateSavedSearchRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new CreateSavedSearchCommand(
                        request.Name,
                        request.EntityType,
                        request.FiltersJson,
                        request.SortBy,
                        request.SortDir);

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Created($"/saved-searches/{result.Id}", result);
                })
            .WithName("CreateSavedSearch")
            .Produces<CreateSavedSearchResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Save a named search filter configuration for the current user")
            .WithTags("Saved Searches")
            .RequireAuthorization();
    }
}

using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.Requests.GetRequests;
using Shared.Pagination;

namespace Api.Endpoints.Requests.GetRequests;

public class GetRequestsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests",
                async (
                    [AsParameters] PaginationRequest request,
                    string? search,
                    string? status,
                    string? purpose,
                    string? sortBy,
                    string? sortDirection,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetRequestQuery(request, search, status, purpose, sortBy, sortDirection);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(new GetRequestsResponse(result.Result));
                })
            .WithName("GetRequests")
            .Produces<GetRequestsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Requests")
            .WithSummary("Get all requests")
            .WithDescription("Retrieves all requests from the system with pagination support.")
            .AllowAnonymous();
    }
}

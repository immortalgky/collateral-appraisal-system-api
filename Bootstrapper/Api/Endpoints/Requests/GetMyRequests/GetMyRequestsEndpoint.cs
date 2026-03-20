using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.Requests.GetMyRequests;
using Request.Application.Features.Requests.GetRequests;
using Shared.Pagination;

namespace Api.Endpoints.Requests.GetMyRequests;

public class GetMyRequestsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/me",
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
                    var query = new GetMyRequestsQuery(request, search, status, purpose, sortBy, sortDirection);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(new GetRequestsResponse(result.Result));
                })
            .WithName("GetMyRequests")
            .Produces<GetRequestsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithTags("Requests")
            .WithSummary("Get current user's requests")
            .WithDescription("Retrieves requests created by the authenticated user with pagination support.")
            .RequireAuthorization();
    }
}

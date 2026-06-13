using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Auth.Application.Features.Scopes.GetScopes;

public class GetScopesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/scopes", async (
                string? search,
                int? pageNumber,
                int? pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetScopesQuery(search, pageNumber ?? 1, pageSize ?? 50);
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetScopes")
            .WithTags("Admin - OAuth Scopes")
            .Produces<PaginatedResult<ScopeDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("OAuthScopesManage");
    }
}

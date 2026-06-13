using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Auth.Application.Features.OAuthTokens.GetTokens;

public class GetTokensEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/tokens", async (
                string? clientId,
                string? subject,
                string? status,
                int? pageNumber,
                int? pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetTokensQuery(
                    clientId, subject, status, pageNumber ?? 1, pageSize ?? 20);
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetTokens")
            .WithTags("Admin - OAuth Tokens")
            .Produces<PaginatedResult<TokenDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("OAuthTokensRevoke");
    }
}

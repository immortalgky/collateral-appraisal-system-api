using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Pagination;

namespace Auth.Application.Features.Scopes.GetScopes;

public record GetScopesQuery(string? Search, int PageNumber, int PageSize)
    : IQuery<PaginatedResult<ScopeDto>>;

public class GetScopesQueryHandler(IOpenIddictScopeManager scopeManager)
    : IQueryHandler<GetScopesQuery, PaginatedResult<ScopeDto>>
{
    public async Task<PaginatedResult<ScopeDto>> Handle(
        GetScopesQuery request,
        CancellationToken cancellationToken)
    {
        var items = new List<ScopeDto>();
        await foreach (var scope in scopeManager.ListAsync(null, null, cancellationToken))
            items.Add(await ScopeProjection.ToDtoAsync(scopeManager, scope, cancellationToken));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            items = items
                .Where(s => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || (s.DisplayName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = items.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var page = ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PaginatedResult<ScopeDto>(page, ordered.Count, request.PageNumber, request.PageSize);
    }
}

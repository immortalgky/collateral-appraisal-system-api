using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Pagination;

namespace Auth.Application.Features.Clients.GetClients;

public record GetClientsQuery(string? Search, int PageNumber, int PageSize)
    : IQuery<PaginatedResult<ClientListItemDto>>;

public class GetClientsQueryHandler(IOpenIddictApplicationManager applicationManager)
    : IQueryHandler<GetClientsQuery, PaginatedResult<ClientListItemDto>>
{
    public async Task<PaginatedResult<ClientListItemDto>> Handle(
        GetClientsQuery request,
        CancellationToken cancellationToken)
    {
        // The client set is tiny (a handful), so projecting + filtering in memory is simplest and
        // avoids leaking OpenIddict store internals into a Dapper query against its schema.
        var items = new List<ClientListItemDto>();
        await foreach (var app in applicationManager.ListAsync(null, null, cancellationToken))
            items.Add(await ClientPermissionMapper.ToDetailDtoAsync(applicationManager, app, cancellationToken));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            items = items
                .Where(c => c.ClientId.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || c.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = items.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        var page = ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PaginatedResult<ClientListItemDto>(
            page, ordered.Count, request.PageNumber, request.PageSize);
    }
}

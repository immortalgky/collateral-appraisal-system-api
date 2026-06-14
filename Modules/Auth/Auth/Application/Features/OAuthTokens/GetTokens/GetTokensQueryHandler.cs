using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Pagination;

namespace Auth.Application.Features.OAuthTokens.GetTokens;

public record GetTokensQuery(
    string? ClientId, string? Subject, string? Status, int PageNumber, int PageSize)
    : IQuery<PaginatedResult<TokenDto>>;

public class GetTokensQueryHandler(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictApplicationManager applicationManager)
    : IQueryHandler<GetTokensQuery, PaginatedResult<TokenDto>>
{
    public async Task<PaginatedResult<TokenDto>> Handle(
        GetTokensQuery request,
        CancellationToken cancellationToken)
    {
        var resolver = new ClientIdResolver(applicationManager);

        // Any filter (client, subject, OR status) requires enumerating the candidate set so the
        // returned Count matches the filtered items. Only the truly-unfiltered case pages at the
        // store level to bound the scan.
        var hasFilter = !string.IsNullOrWhiteSpace(request.ClientId)
                        || !string.IsNullOrWhiteSpace(request.Subject)
                        || !string.IsNullOrWhiteSpace(request.Status);

        if (hasFilter)
        {
            var all = new List<TokenDto>();
            await foreach (var token in FindFilteredAsync(request, cancellationToken))
            {
                // Filter on the cheap status read BEFORE the full projection (which resolves the
                // client id and reads several fields) so a status filter doesn't project rows it discards.
                if (!string.IsNullOrWhiteSpace(request.Status)
                    && !string.Equals(await tokenManager.GetStatusAsync(token, cancellationToken),
                        request.Status, StringComparison.OrdinalIgnoreCase))
                    continue;

                all.Add(await ProjectAsync(token, resolver, cancellationToken));
            }

            var ordered = all.OrderByDescending(t => t.CreationDate).ToList();
            var page = ordered
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();
            return new PaginatedResult<TokenDto>(page, ordered.Count, request.PageNumber, request.PageSize);
        }

        var total = await tokenManager.CountAsync(cancellationToken);
        var offset = (request.PageNumber - 1) * request.PageSize;
        var items = new List<TokenDto>();
        await foreach (var token in tokenManager.ListAsync(request.PageSize, offset, cancellationToken))
            items.Add(await ProjectAsync(token, resolver, cancellationToken));

        return new PaginatedResult<TokenDto>(items, total, request.PageNumber, request.PageSize);
    }

    private async Task<TokenDto> ProjectAsync(
        object token, ClientIdResolver resolver, CancellationToken cancellationToken)
    {
        var appId = await tokenManager.GetApplicationIdAsync(token, cancellationToken);
        return new TokenDto
        {
            Id = await tokenManager.GetIdAsync(token, cancellationToken) ?? "",
            Subject = await tokenManager.GetSubjectAsync(token, cancellationToken),
            Status = await tokenManager.GetStatusAsync(token, cancellationToken),
            Type = await tokenManager.GetTypeAsync(token, cancellationToken),
            ApplicationId = appId,
            ClientId = await resolver.ResolveAsync(appId, cancellationToken),
            CreationDate = await tokenManager.GetCreationDateAsync(token, cancellationToken),
            ExpirationDate = await tokenManager.GetExpirationDateAsync(token, cancellationToken)
        };
    }

    // Yields the candidate set for the filtered path, using the most selective store finder
    // available. A status-only filter enumerates the full set (the caller then filters by status).
    private async IAsyncEnumerable<object> FindFilteredAsync(
        GetTokensQuery request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ClientId))
        {
            var app = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
            var appId = app is null ? null : await applicationManager.GetIdAsync(app, cancellationToken);
            if (appId is null) yield break;
            await foreach (var t in tokenManager.FindByApplicationIdAsync(appId, cancellationToken))
                yield return t;
        }
        else if (!string.IsNullOrWhiteSpace(request.Subject))
        {
            await foreach (var t in tokenManager.FindBySubjectAsync(request.Subject, cancellationToken))
                yield return t;
        }
        else
        {
            await foreach (var t in tokenManager.ListAsync(null, null, cancellationToken))
                yield return t;
        }
    }
}

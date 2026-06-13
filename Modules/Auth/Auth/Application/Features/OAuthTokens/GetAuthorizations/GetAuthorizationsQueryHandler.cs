using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Pagination;

namespace Auth.Application.Features.OAuthTokens.GetAuthorizations;

public record GetAuthorizationsQuery(
    string? ClientId, string? Subject, string? Status, int PageNumber, int PageSize)
    : IQuery<PaginatedResult<AuthorizationDto>>;

public class GetAuthorizationsQueryHandler(
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictApplicationManager applicationManager)
    : IQueryHandler<GetAuthorizationsQuery, PaginatedResult<AuthorizationDto>>
{
    public async Task<PaginatedResult<AuthorizationDto>> Handle(
        GetAuthorizationsQuery request,
        CancellationToken cancellationToken)
    {
        var resolver = new ClientIdResolver(applicationManager);

        // Any filter (client, subject, OR status) requires enumerating the candidate set so the
        // returned Count matches the filtered items — otherwise store-level CountAsync would report
        // an inflated total and the UI would show empty trailing pages. Only the truly-unfiltered
        // case pages at the store level to bound the scan.
        var hasFilter = !string.IsNullOrWhiteSpace(request.ClientId)
                        || !string.IsNullOrWhiteSpace(request.Subject)
                        || !string.IsNullOrWhiteSpace(request.Status);

        if (hasFilter)
        {
            var all = new List<AuthorizationDto>();
            await foreach (var auth in FindFilteredAsync(request, cancellationToken))
            {
                // Filter on the cheap status read BEFORE the full projection (which resolves the
                // client id and reads several fields) so a status filter doesn't project rows it discards.
                if (!string.IsNullOrWhiteSpace(request.Status)
                    && !string.Equals(await authorizationManager.GetStatusAsync(auth, cancellationToken),
                        request.Status, StringComparison.OrdinalIgnoreCase))
                    continue;

                all.Add(await ProjectAsync(auth, resolver, cancellationToken));
            }

            var ordered = all.OrderByDescending(a => a.CreationDate).ToList();
            var page = ordered
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();
            return new PaginatedResult<AuthorizationDto>(page, ordered.Count, request.PageNumber, request.PageSize);
        }

        var total = await authorizationManager.CountAsync(cancellationToken);
        var offset = (request.PageNumber - 1) * request.PageSize;
        var items = new List<AuthorizationDto>();
        await foreach (var auth in authorizationManager.ListAsync(request.PageSize, offset, cancellationToken))
            items.Add(await ProjectAsync(auth, resolver, cancellationToken));

        return new PaginatedResult<AuthorizationDto>(items, total, request.PageNumber, request.PageSize);
    }

    private async Task<AuthorizationDto> ProjectAsync(
        object auth, ClientIdResolver resolver, CancellationToken cancellationToken)
    {
        var appId = await authorizationManager.GetApplicationIdAsync(auth, cancellationToken);
        return new AuthorizationDto
        {
            Id = await authorizationManager.GetIdAsync(auth, cancellationToken) ?? "",
            Subject = await authorizationManager.GetSubjectAsync(auth, cancellationToken),
            Status = await authorizationManager.GetStatusAsync(auth, cancellationToken),
            Type = await authorizationManager.GetTypeAsync(auth, cancellationToken),
            ApplicationId = appId,
            ClientId = await resolver.ResolveAsync(appId, cancellationToken),
            Scopes = [.. (await authorizationManager.GetScopesAsync(auth, cancellationToken))],
            CreationDate = await authorizationManager.GetCreationDateAsync(auth, cancellationToken)
        };
    }

    // Yields the candidate set for the filtered path, using the most selective store finder
    // available. A status-only filter has no selective finder, so it enumerates the full set
    // (the caller then filters by status) — accepted cost for an uncommon admin query.
    private async IAsyncEnumerable<object> FindFilteredAsync(
        GetAuthorizationsQuery request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ClientId))
        {
            var app = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
            var appId = app is null ? null : await applicationManager.GetIdAsync(app, cancellationToken);
            if (appId is null) yield break;
            await foreach (var a in authorizationManager.FindByApplicationIdAsync(appId, cancellationToken))
                yield return a;
        }
        else if (!string.IsNullOrWhiteSpace(request.Subject))
        {
            await foreach (var a in authorizationManager.FindBySubjectAsync(request.Subject, cancellationToken))
                yield return a;
        }
        else
        {
            await foreach (var a in authorizationManager.ListAsync(null, null, cancellationToken))
                yield return a;
        }
    }
}

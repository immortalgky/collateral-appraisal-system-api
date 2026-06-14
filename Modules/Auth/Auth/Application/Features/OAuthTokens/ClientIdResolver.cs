using OpenIddict.Abstractions;

namespace Auth.Application.Features.OAuthTokens;

/// <summary>
/// Resolves an OpenIddict application primary-key id to its human-readable ClientId,
/// memoised so a page of tokens for the same client only hits the store once.
/// </summary>
public sealed class ClientIdResolver(IOpenIddictApplicationManager applicationManager)
{
    private readonly Dictionary<string, string?> _cache = new();

    public async Task<string?> ResolveAsync(string? applicationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(applicationId)) return null;
        if (_cache.TryGetValue(applicationId, out var cached)) return cached;

        var app = await applicationManager.FindByIdAsync(applicationId, cancellationToken);
        var clientId = app is null ? null : await applicationManager.GetClientIdAsync(app, cancellationToken);
        _cache[applicationId] = clientId;
        return clientId;
    }
}

using Auth.Application.Features.Clients;
using Auth.Contracts.Clients;
using OpenIddict.Abstractions;

namespace Auth.Application.Services;

/// <summary>
/// Implements <see cref="IClientLookupService"/> over the OpenIddict application manager, reading
/// the same setting the admin clients screen writes via <see cref="ClientPermissionMapper"/>.
/// </summary>
public class ClientLookupService(IOpenIddictApplicationManager applicationManager) : IClientLookupService
{
    public async Task<TimeSpan?> GetUploadSessionLifetimeAsync(string clientId, CancellationToken ct = default)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId, ct);

        if (application is null) return null;

        var settings = await applicationManager.GetSettingsAsync(application, ct);

        return ClientPermissionMapper.ReadUploadSessionLifetime(settings);
    }
}

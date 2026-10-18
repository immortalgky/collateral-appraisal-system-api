namespace Auth.Contracts.Clients;

/// <summary>
/// Cross-module contract for per-client settings stored on the OAuth client (auth.OpenIddictApplications).
/// Implemented in the Auth module, consumed by modules that act on behalf of a calling client —
/// e.g. Integration, when sizing the upload session an external system asks for.
/// </summary>
public interface IClientLookupService
{
    /// <summary>
    /// How long an upload session created by this client stays open, or null when the client does
    /// not exist or has no (usable) setting of its own — the caller then applies its own default.
    /// </summary>
    Task<TimeSpan?> GetUploadSessionLifetimeAsync(string clientId, CancellationToken ct = default);
}

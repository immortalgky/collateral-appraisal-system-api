namespace Auth.Application.Features.Clients;

/// <summary>
/// Validation predicates shared by the register and update client commands so the two paths
/// can never diverge.
/// </summary>
public static class ClientValidationRules
{
    public static bool IsKnownGrantType(string grantType) =>
        ClientPermissionMapper.AllGrantTypes.Contains(grantType, StringComparer.OrdinalIgnoreCase);

    /// <summary>Redirect/post-logout URIs must be absolute http(s) — rejects relative and javascript:/data: schemes.</summary>
    public static bool IsAbsoluteHttpUri(Uri uri) =>
        uri is { IsAbsoluteUri: true }
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static readonly string RefreshTokenLifetimeMessage =
        $"Refresh token lifetime must be between {(int)ClientPermissionMapper.MinRefreshTokenLifetime.TotalMinutes} " +
        $"and {(int)ClientPermissionMapper.MaxRefreshTokenLifetime.TotalMinutes} minutes, or empty to use the server default.";

    /// <summary>
    /// Bounds the per-client refresh-token lifetime. Null is valid and means "fall back to the
    /// server default"; the caller gates on HasValue before invoking this.
    /// </summary>
    public static bool IsValidRefreshTokenLifetime(int? minutes) =>
        minutes is { } value
        && value >= (int)ClientPermissionMapper.MinRefreshTokenLifetime.TotalMinutes
        && value <= (int)ClientPermissionMapper.MaxRefreshTokenLifetime.TotalMinutes;
}

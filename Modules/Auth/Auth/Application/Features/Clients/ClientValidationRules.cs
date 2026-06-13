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
}

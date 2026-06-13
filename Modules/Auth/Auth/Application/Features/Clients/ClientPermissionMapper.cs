using OpenIddict.Abstractions;
using Consts = OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth.Application.Features.Clients;

/// <summary>
/// Translates between the friendly admin-screen shape (grant types + scopes) and the opaque
/// OpenIddict permission strings (ept:/gt:/rt:/scp:). The UI never sees the raw permission codes.
/// </summary>
public static class ClientPermissionMapper
{
    public const string GrantAuthorizationCode = "authorization_code";
    public const string GrantClientCredentials = "client_credentials";
    public const string GrantPassword = "password";
    public const string GrantRefreshToken = "refresh_token";

    public static readonly string[] AllGrantTypes =
        [GrantAuthorizationCode, GrantClientCredentials, GrantPassword, GrantRefreshToken];

    public static readonly HashSet<string> SystemClientIds =
        new(StringComparer.OrdinalIgnoreCase) { "spa", "los", "cls" };

    /// <summary>Normalises "public"/"confidential" (any case) to the OpenIddict constant.</summary>
    public static string NormalizeClientType(string clientType) =>
        clientType.Equals(Consts.ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase)
            ? Consts.ClientTypes.Confidential
            : Consts.ClientTypes.Public;

    public static bool IsConfidential(string clientType) =>
        clientType.Equals(Consts.ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the OpenIddict permission set from friendly grant types + scopes.</summary>
    public static HashSet<string> BuildPermissions(
        IEnumerable<string> grantTypes,
        IEnumerable<string> scopes,
        bool hasPostLogout)
    {
        var gts = grantTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var perms = new HashSet<string>
        {
            // Every token-issuing grant goes through the token endpoint.
            Consts.Permissions.Endpoints.Token
        };

        if (gts.Contains(GrantAuthorizationCode))
        {
            perms.Add(Consts.Permissions.Endpoints.Authorization);
            perms.Add(Consts.Permissions.GrantTypes.AuthorizationCode);
            perms.Add(Consts.Permissions.ResponseTypes.Code);
        }

        if (gts.Contains(GrantClientCredentials))
            perms.Add(Consts.Permissions.GrantTypes.ClientCredentials);

        if (gts.Contains(GrantPassword))
            perms.Add(Consts.Permissions.GrantTypes.Password);

        if (gts.Contains(GrantRefreshToken))
            perms.Add(Consts.Permissions.GrantTypes.RefreshToken);

        if (hasPostLogout)
            perms.Add(Consts.Permissions.Endpoints.EndSession);

        foreach (var scope in scopes.Where(s => !string.IsNullOrWhiteSpace(s)))
            perms.Add(Consts.Permissions.Prefixes.Scope + scope);

        return perms;
    }

    public static List<string> ExtractGrantTypes(IEnumerable<string> permissions)
    {
        var set = permissions.ToHashSet();
        var result = new List<string>();
        if (set.Contains(Consts.Permissions.GrantTypes.AuthorizationCode)) result.Add(GrantAuthorizationCode);
        if (set.Contains(Consts.Permissions.GrantTypes.ClientCredentials)) result.Add(GrantClientCredentials);
        if (set.Contains(Consts.Permissions.GrantTypes.Password)) result.Add(GrantPassword);
        if (set.Contains(Consts.Permissions.GrantTypes.RefreshToken)) result.Add(GrantRefreshToken);
        return result;
    }

    public static List<string> ExtractScopes(IEnumerable<string> permissions)
    {
        var prefix = Consts.Permissions.Prefixes.Scope;
        return permissions
            .Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
            .Select(p => p[prefix.Length..])
            .ToList();
    }

    /// <summary>
    /// Applies the friendly fields (everything except ClientId/ClientSecret) onto a descriptor.
    /// Used by both create and update so the two paths can never drift.
    /// </summary>
    public static void ApplyToDescriptor(
        OpenIddictApplicationDescriptor descriptor,
        string clientType,
        IReadOnlyCollection<string> grantTypes,
        IEnumerable<string> scopes,
        IReadOnlyCollection<Uri> redirectUris,
        IReadOnlyCollection<Uri> postLogoutRedirectUris)
    {
        descriptor.RedirectUris.Clear();
        descriptor.RedirectUris.UnionWith(redirectUris);

        descriptor.PostLogoutRedirectUris.Clear();
        descriptor.PostLogoutRedirectUris.UnionWith(postLogoutRedirectUris);

        descriptor.Permissions.Clear();
        descriptor.Permissions.UnionWith(
            BuildPermissions(grantTypes, scopes, postLogoutRedirectUris.Count > 0));

        descriptor.Requirements.Clear();
        // PKCE is required for public clients using the authorization-code flow.
        if (!IsConfidential(clientType)
            && grantTypes.Contains(GrantAuthorizationCode, StringComparer.OrdinalIgnoreCase))
            descriptor.Requirements.Add(Consts.Requirements.Features.ProofKeyForCodeExchange);
    }

    /// <summary>Projects an OpenIddict application instance into the friendly detail DTO.</summary>
    public static async Task<ClientDetailDto> ToDetailDtoAsync(
        IOpenIddictApplicationManager manager,
        object application,
        CancellationToken cancellationToken)
    {
        var clientId = await manager.GetClientIdAsync(application, cancellationToken) ?? "";
        var clientType = await manager.GetClientTypeAsync(application, cancellationToken) ?? Consts.ClientTypes.Public;
        var permissions = await manager.GetPermissionsAsync(application, cancellationToken);

        return new ClientDetailDto
        {
            Id = await manager.GetIdAsync(application, cancellationToken) ?? "",
            ClientId = clientId,
            DisplayName = await manager.GetDisplayNameAsync(application, cancellationToken) ?? "",
            ClientType = clientType,
            RedirectUris = [.. (await manager.GetRedirectUrisAsync(application, cancellationToken))],
            PostLogoutRedirectUris = [.. (await manager.GetPostLogoutRedirectUrisAsync(application, cancellationToken))],
            GrantTypes = ExtractGrantTypes(permissions),
            Scopes = ExtractScopes(permissions),
            HasSecret = IsConfidential(clientType),
            IsSystem = SystemClientIds.Contains(clientId)
        };
    }
}

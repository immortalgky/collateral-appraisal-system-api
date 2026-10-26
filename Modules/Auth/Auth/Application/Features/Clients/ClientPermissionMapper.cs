using System.Globalization;
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
    public const string GrantRefreshToken = "refresh_token";

    public static readonly string[] AllGrantTypes =
        [GrantAuthorizationCode, GrantClientCredentials, GrantRefreshToken];

    public static readonly HashSet<string> SystemClientIds =
        new(StringComparer.OrdinalIgnoreCase) { "spa" };

    /// <summary>
    /// A lifetime an admin may override for a single client (a token lifetime, or the upload-session
    /// lifetime). OpenIddict reads the token settings straight off the application row and parses
    /// them with TimeSpan.Parse; the value it cannot parse is ignored silently in favour of the
    /// server-wide default, which is why <see cref="Min"/>/<see cref="Max"/> are enforced before
    /// anything is stored.
    /// </summary>
    public sealed record ClientLifetimeKind(string SettingKey, TimeSpan Min, TimeSpan Max)
    {
        public int MinMinutes => (int)Min.TotalMinutes;
        public int MaxMinutes => (int)Max.TotalMinutes;
    }

    /// <summary>Below a minute nothing is usable; the upper bounds are guards against a typo adding a digit.</summary>
    private static readonly TimeSpan MinLifetime = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Access and identity tokens are bearer credentials that cannot be revoked before they expire,
    /// so they get a much tighter ceiling than the refresh token — which is a reference token, is
    /// rotated on every use, and is checked against the database each time.
    /// </summary>
    public static readonly ClientLifetimeKind AccessTokenLifetime =
        new(Consts.Settings.TokenLifetimes.AccessToken, MinLifetime, TimeSpan.FromHours(24));

    public static readonly ClientLifetimeKind IdentityTokenLifetime =
        new(Consts.Settings.TokenLifetimes.IdentityToken, MinLifetime, TimeSpan.FromHours(24));

    public static readonly ClientLifetimeKind RefreshTokenLifetime =
        new(Consts.Settings.TokenLifetimes.RefreshToken, MinLifetime, TimeSpan.FromDays(30));

    /// <summary>
    /// How long an upload session created by this client (POST /api/v1/upload-sessions) stays open.
    /// Not an OpenIddict setting: the key is our own and OpenIddict stores it without interpreting it.
    /// The admin screen and API work in whole hours; the stored value is a TimeSpan string like the
    /// token lifetimes above.
    /// </summary>
    public static readonly ClientLifetimeKind UploadSessionLifetime =
        new("cas:upload_session_lifetime", TimeSpan.FromHours(1), TimeSpan.FromDays(30));

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
        IReadOnlyCollection<Uri> postLogoutRedirectUris,
        ClientTokenLifetimes tokenLifetimes,
        int? uploadSessionLifetimeHours)
    {
        ApplyLifetime(descriptor, AccessTokenLifetime, tokenLifetimes.AccessTokenLifetimeMinutes);
        ApplyLifetime(descriptor, IdentityTokenLifetime, tokenLifetimes.IdentityTokenLifetimeMinutes);
        ApplyLifetime(descriptor, RefreshTokenLifetime, tokenLifetimes.RefreshTokenLifetimeMinutes);
        ApplyLifetime(descriptor, UploadSessionLifetime, uploadSessionLifetimeHours * 60L);

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

    /// <summary>
    /// Writes one lifetime onto the descriptor. Removing the key is what "use the server default"
    /// means — writing an empty string instead would leave a value TimeSpan.Parse rejects, so
    /// OpenIddict would fall back to the global default anyway while the row claimed a setting that
    /// does nothing.
    /// </summary>
    private static void ApplyLifetime(
        OpenIddictApplicationDescriptor descriptor, ClientLifetimeKind kind, long? minutes)
    {
        if (minutes is null)
            descriptor.Settings.Remove(kind.SettingKey);
        else
            descriptor.Settings[kind.SettingKey] = TimeSpan.FromMinutes(minutes.Value).ToString();
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
        var settings = await manager.GetSettingsAsync(application, cancellationToken);

        return new ClientDetailDto
        {
            AccessTokenLifetimeMinutes = ReadLifetimeMinutes(settings, AccessTokenLifetime),
            IdentityTokenLifetimeMinutes = ReadLifetimeMinutes(settings, IdentityTokenLifetime),
            RefreshTokenLifetimeMinutes = ReadLifetimeMinutes(settings, RefreshTokenLifetime),
            UploadSessionLifetimeHours = (int?)ReadUploadSessionLifetime(settings)?.TotalHours,
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

    /// <summary>
    /// Projects the stored setting back to whole minutes for the admin UI. An unparsable or absent
    /// value reads as null — the same thing OpenIddict itself does with it, so the screen shows the
    /// lifetime that is actually in force rather than a value that silently does nothing.
    /// <para>
    /// The screen works in whole minutes, so a value written directly into the row with finer
    /// precision is rounded rather than truncated — truncating would render 00:00:45 as "0", which
    /// reads as corruption rather than rounding. Re-saving normalises the stored value.
    /// </para>
    /// <para>
    /// A zero or negative lifetime reads as null rather than being clamped up to 1: OpenIddict would
    /// honour it and mint already-expired tokens, so reporting "1 minute" would be a lie about what
    /// is in force. null at least says "not a value this screen can represent".
    /// </para>
    /// </summary>
    private static int? ReadLifetimeMinutes(
        IReadOnlyDictionary<string, string> settings, ClientLifetimeKind kind)
    {
        if (!settings.TryGetValue(kind.SettingKey, out var setting)
            || !TimeSpan.TryParse(setting, CultureInfo.InvariantCulture, out var lifetime)
            || lifetime <= TimeSpan.Zero)
            return null;

        return (int)Math.Round(lifetime.TotalMinutes, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The upload-session lifetime in force, or null for "use the server default". Anything the admin
    /// API would not have accepted — outside the kind's bounds or not a whole number of hours — reads
    /// as null, so a value written straight into the row can neither be echoed back to the screen
    /// (where re-saving would 400 or silently re-round it) nor stretch a session past the ceiling.
    /// Shared with the cross-module lookup so the screen and the consumer cannot disagree.
    /// <para>
    /// Only the invariant "c" form with a time part is read: TimeSpan.Parse takes a bare "24" as 24
    /// days, which no admin screen write can produce but a hand-edited row could.
    /// </para>
    /// </summary>
    public static TimeSpan? ReadUploadSessionLifetime(IReadOnlyDictionary<string, string> settings) =>
        settings.TryGetValue(UploadSessionLifetime.SettingKey, out var setting)
        && setting.Contains(':')
        && TimeSpan.TryParseExact(setting, "c", CultureInfo.InvariantCulture, out var lifetime)
        && IsValidUploadSessionLifetime(lifetime)
            ? lifetime
            : null;

    /// <summary>
    /// Both this reader and the admin validator derive their bounds from
    /// <see cref="UploadSessionLifetime"/> Min/Max; the reader additionally rejects stored strings
    /// that are not a whole number of hours, which the int-hours API cannot produce.
    /// </summary>
    private static bool IsValidUploadSessionLifetime(TimeSpan lifetime) =>
        lifetime >= UploadSessionLifetime.Min
        && lifetime <= UploadSessionLifetime.Max
        && lifetime.Ticks % TimeSpan.TicksPerHour == 0;
}

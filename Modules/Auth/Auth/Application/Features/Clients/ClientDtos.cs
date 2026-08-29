namespace Auth.Application.Features.Clients;

/// <summary>
/// Friendly projection of an OpenIddict application for admin list screens.
/// Never carries the client secret — confidential clients only expose <see cref="HasSecret"/>.
/// </summary>
public class ClientListItemDto
{
    public string Id { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string ClientType { get; set; } = default!;
    public List<string> GrantTypes { get; set; } = [];
    public List<string> Scopes { get; set; } = [];
    public bool HasSecret { get; set; }

    /// <summary>Seeded core clients (spa/los/cls) cannot be deleted from the UI.</summary>
    public bool IsSystem { get; set; }
}

public class ClientDetailDto : ClientListItemDto
{
    public List<string> RedirectUris { get; set; } = [];
    public List<string> PostLogoutRedirectUris { get; set; } = [];

    /// <summary>
    /// How long an access token issued to this client stays valid, in minutes. A bearer credential
    /// that cannot be revoked before it expires, so shorter is safer.
    /// </summary>
    public int? AccessTokenLifetimeMinutes { get; set; }

    /// <summary>How long an identity (ID) token issued to this client stays valid, in minutes.</summary>
    public int? IdentityTokenLifetimeMinutes { get; set; }

    /// <summary>
    /// How long a refresh token issued to this client stays valid, in minutes. Because refresh
    /// tokens are rolling and sliding, this is an idle timeout — the window a session may go
    /// without refreshing before the user must sign in again.
    /// </summary>
    public int? RefreshTokenLifetimeMinutes { get; set; }
}

/// <summary>
/// The three per-client token lifetimes, in minutes. null on any of them means "this client has no
/// setting of its own and inherits the server-wide default" — see AuthModule.
/// <para>
/// Carried as a group so create and update cannot drift, and expressed in minutes rather than as
/// .NET TimeSpan strings so the admin UI never has to ask anyone to type "7.00:00:00" correctly.
/// </para>
/// </summary>
public record ClientTokenLifetimes(
    int? AccessTokenLifetimeMinutes,
    int? IdentityTokenLifetimeMinutes,
    int? RefreshTokenLifetimeMinutes);

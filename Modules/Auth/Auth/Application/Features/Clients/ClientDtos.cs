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
    /// How long a refresh token issued to this client stays valid, in minutes. Because refresh
    /// tokens are rolling and sliding, this is an idle timeout — the window a session may go
    /// without refreshing before the user must sign in again.
    /// <para>
    /// null means the client has no setting of its own and falls back to the server-wide default.
    /// Exposed in minutes rather than as a .NET TimeSpan string so the admin UI never has to ask
    /// anyone to type "7.00:00:00" correctly.
    /// </para>
    /// </summary>
    public int? RefreshTokenLifetimeMinutes { get; set; }
}

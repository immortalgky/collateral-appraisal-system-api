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
}

namespace OAuth2OpenId.Identity.Dtos;

public record RegisterClientDto
{
    public string DisplayName;
    public string ClientType;
    public List<Uri> PostLogoutRedirectUris;
    public List<Uri> RedirectUris;
    public List<string> Permissions;
    public List<string> Requirements;
}
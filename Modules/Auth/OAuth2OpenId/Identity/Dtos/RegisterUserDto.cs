namespace OAuth2OpenId.Identity.Dtos;

public record RegisterUserDto
{
    public string Username;
    public string Password;
    public string Email;
    public List<string> Permissions;
}
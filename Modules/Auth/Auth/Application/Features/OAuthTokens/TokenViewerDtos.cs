namespace Auth.Application.Features.OAuthTokens;

public class AuthorizationDto
{
    public string Id { get; set; } = default!;
    public string? Subject { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? ApplicationId { get; set; }
    public string? ClientId { get; set; }
    public List<string> Scopes { get; set; } = [];
    public DateTimeOffset? CreationDate { get; set; }
}

public class TokenDto
{
    public string Id { get; set; } = default!;
    public string? Subject { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? ApplicationId { get; set; }
    public string? ClientId { get; set; }
    public DateTimeOffset? CreationDate { get; set; }
    public DateTimeOffset? ExpirationDate { get; set; }
}

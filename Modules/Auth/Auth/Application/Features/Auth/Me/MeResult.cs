namespace Auth.Domain.Auth.Features.Me;

public record MeResult(
    Guid Id,
    string Username,
    string? Email,
    List<string> Roles,
    List<string> Permissions
);

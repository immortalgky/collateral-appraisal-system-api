namespace Auth.Domain.Auth.Features.Me;

public record MeResponse(
    Guid Id,
    string Username,
    string? Email,
    List<string> Roles,
    List<string> Permissions
);

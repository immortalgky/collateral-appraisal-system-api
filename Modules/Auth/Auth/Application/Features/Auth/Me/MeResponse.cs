namespace Auth.Domain.Auth.Features.Me;

public record MeResponse(
    Guid Id,
    string Username,
    string? Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? Position,
    string? Department,
    List<string> Roles,
    List<string> Permissions
);

namespace OAuth2OpenId.Domain.Identity.Dtos;

public record RegisterUserDto(
    string Username,
    string Password,
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? Position,
    string? Department,
    List<RegisterUserPermissionDto> Permissions,
    List<Guid> Roles
);

namespace OAuth2OpenId.Domain.Identity.Dtos;

public record RegisterUserDto(
    string Username,
    string Password,
    string Email,
    List<RegisterUserPermissionDto> Permissions,
    List<Guid> Roles
);

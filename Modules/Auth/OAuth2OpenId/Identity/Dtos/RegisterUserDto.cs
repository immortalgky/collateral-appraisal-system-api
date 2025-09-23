namespace OAuth2OpenId.Identity.Dtos;

public record RegisterUserDto(
    string Username,
    string Password,
    string Email,
    List<Guid> Permissions,
    List<Guid> Roles
);

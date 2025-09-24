namespace OAuth2OpenId.Identity.Dtos;

public record CreateRoleDto(string Name, string Description, List<Guid> Permissions);

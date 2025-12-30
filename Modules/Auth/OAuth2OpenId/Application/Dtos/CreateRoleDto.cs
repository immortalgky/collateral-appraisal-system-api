namespace OAuth2OpenId.Domain.Identity.Dtos;

public record CreateRoleDto(string Name, string Description, List<Guid> Permissions);

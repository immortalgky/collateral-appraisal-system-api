namespace OAuth2OpenId.Identity.Dtos;

public record RoleDto(Guid Id, string Name, string Description, List<PermissionDto> Permissions);

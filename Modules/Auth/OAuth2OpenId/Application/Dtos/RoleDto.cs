namespace OAuth2OpenId.Domain.Identity.Dtos;

public record RoleDto(Guid Id, string Name, string Description, List<PermissionDto> Permissions);

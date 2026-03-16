namespace Auth.Application.Dtos;

public record RoleDto(Guid Id, string Name, string Description, List<PermissionDto> Permissions);

namespace Auth.Application.Dtos;

public record CreateRoleDto(string Name, string Description, List<Guid> Permissions);

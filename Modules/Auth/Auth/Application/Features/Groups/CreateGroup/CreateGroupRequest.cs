namespace Auth.Application.Features.Groups.CreateGroup;

public record CreateGroupRequest(string Name, string Description, string Scope, Guid? CompanyId);

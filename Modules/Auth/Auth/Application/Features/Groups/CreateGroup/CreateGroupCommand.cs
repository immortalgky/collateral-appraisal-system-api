namespace Auth.Application.Features.Groups.CreateGroup;

public record CreateGroupCommand(string Name, string Description, string Scope, Guid? CompanyId)
    : ICommand<CreateGroupResult>;

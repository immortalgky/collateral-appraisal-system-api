namespace Auth.Application.Features.Groups.UpdateGroup;

public record UpdateGroupCommand(Guid Id, string Name, string Description) : ICommand;

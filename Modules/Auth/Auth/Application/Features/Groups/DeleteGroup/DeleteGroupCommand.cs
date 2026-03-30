namespace Auth.Application.Features.Groups.DeleteGroup;

public record DeleteGroupCommand(Guid Id, Guid? DeletedBy) : ICommand;

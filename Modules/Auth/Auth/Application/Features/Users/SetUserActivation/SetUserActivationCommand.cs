namespace Auth.Application.Features.Users.SetUserActivation;

public record SetUserActivationCommand(Guid UserId, bool IsActive) : ICommand;

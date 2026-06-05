namespace Auth.Application.Features.Users.UnlockUser;

public record UnlockUserCommand(Guid UserId) : ICommand;

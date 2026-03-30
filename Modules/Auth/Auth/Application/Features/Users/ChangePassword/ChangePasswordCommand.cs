namespace Auth.Application.Features.Users.ChangePassword;

public record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword, string ConfirmPassword) : ICommand;

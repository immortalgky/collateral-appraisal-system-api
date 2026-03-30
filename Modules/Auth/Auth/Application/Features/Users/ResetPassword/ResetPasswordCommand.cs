namespace Auth.Application.Features.Users.ResetPassword;

public record ResetPasswordCommand(Guid UserId, string NewPassword, string ConfirmPassword) : ICommand;

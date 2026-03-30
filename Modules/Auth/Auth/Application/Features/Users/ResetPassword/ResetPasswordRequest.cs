namespace Auth.Application.Features.Users.ResetPassword;

public record ResetPasswordRequest(string NewPassword, string ConfirmPassword);

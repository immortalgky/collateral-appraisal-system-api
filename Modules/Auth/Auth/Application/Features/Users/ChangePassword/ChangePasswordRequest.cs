namespace Auth.Application.Features.Users.ChangePassword;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

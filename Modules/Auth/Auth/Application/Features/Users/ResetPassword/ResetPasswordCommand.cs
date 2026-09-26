namespace Auth.Application.Features.Users.ResetPassword;

public record ResetPasswordCommand(Guid UserId, string NewPassword, string ConfirmPassword) : ICommand
{
    /// <summary>Redacts the passwords — see ChangePasswordCommand.ToString.</summary>
    public override string ToString() =>
        $"{nameof(ResetPasswordCommand)} {{ UserId = {UserId}, {nameof(NewPassword)} = ***, {nameof(ConfirmPassword)} = *** }}";
}

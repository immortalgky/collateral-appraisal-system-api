namespace Auth.Application.Features.Users.ChangePassword;

public record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword, string ConfirmPassword) : ICommand
{
    /// <summary>
    /// Redacts the passwords: LoggingBehavior logs every request at Information, and the default record
    /// ToString would write them to Seq and the application log table in plaintext.
    /// </summary>
    public override string ToString() =>
        $"{nameof(ChangePasswordCommand)} {{ UserId = {UserId}, CurrentPassword = ***, NewPassword = ***, ConfirmPassword = *** }}";
}

using Auth.Application.Services;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.ChangePassword;

public class ChangePasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IPasswordHistoryRecorder passwordHistoryRecorder)
    : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Unit> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        if (AuthSources.IsLdap(user.AuthSource))
            throw new InvalidOperationException("Password change is only available for local accounts.");

        var result = await userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        // Clear the must-change-password flag if it was set (e.g. after admin reset or expiry)
        user.MustChangePassword = false;

        // Stamp PasswordChangedAt + append to history (persists the cleared flag too).
        await passwordHistoryRecorder.RecordAsync(user, cancellationToken);

        return Unit.Value;
    }
}

using Auth.Application.Services;
using Auth.Domain.Sessions;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.ChangePassword;

public class ChangePasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IPasswordHistoryRecorder passwordHistoryRecorder,
    IUserSessionService sessions,
    AuthorizationClaimsBuilder claims,
    IHttpContextAccessor httpContextAccessor)
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

        // End the user's other sessions, but keep the one that made the change: a user changing their own
        // password stays signed in here. An admin changing someone else's ends all of theirs. The audit
        // entry this enqueues is flushed by the save below.
        var caller = httpContextAccessor.HttpContext?.User;
        var currentSession = caller?.FindFirst("sub")?.Value == user.Id.ToString()
                             && Guid.TryParse(caller.FindFirst(AuthSchemes.SessionIdClaim)?.Value, out var sid)
            ? sid
            : (Guid?)null;
        await sessions.RevokeUserAsync(user.Id, user.UserName, SessionEndReason.PasswordChanged, currentSession,
            cancellationToken);

        // Stamp PasswordChangedAt + append to history (persists the cleared flag too).
        await passwordHistoryRecorder.RecordAsync(user, cancellationToken);

        // Only now is the cleared flag in the database. Dropping the cached view any earlier would let a
        // concurrent request re-read the old flag and cache it again.
        claims.Invalidate(user.Id);

        return Unit.Value;
    }
}

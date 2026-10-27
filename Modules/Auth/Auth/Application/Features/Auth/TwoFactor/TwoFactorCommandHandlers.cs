using Auth.Application.Features.Users.ResetTwoFactor;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Auth.TwoFactor;

// A wrong code is a BadRequestException (400, with the message as `detail`), never a 401: the SPA signs the user
// out on any 401.

public abstract class TwoFactorHandler(UserManager<ApplicationUser> userManager)
{
    protected async Task<ApplicationUser> GetUserAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User", userId);
}

public class BeginTwoFactorSetupCommandHandler(UserManager<ApplicationUser> userManager, TwoFactorService twoFactor)
    : TwoFactorHandler(userManager), ICommandHandler<BeginTwoFactorSetupCommand, TwoFactorSetupInfo>
{
    // An unverified key from an earlier, unfinished attempt is reused: the QR code already in the user's app stays valid.
    public async Task<TwoFactorSetupInfo> Handle(BeginTwoFactorSetupCommand command, CancellationToken cancellationToken) =>
        await twoFactor.BeginSetupAsync(await GetUserAsync(command.UserId));
}

public class EnableTwoFactorCommandHandler(
    UserManager<ApplicationUser> userManager,
    TwoFactorService twoFactor,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext)
    : TwoFactorHandler(userManager), ICommandHandler<EnableTwoFactorCommand, RecoveryCodesResult>
{
    public async Task<RecoveryCodesResult> Handle(EnableTwoFactorCommand command, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(command.UserId);
        var codes = await twoFactor.EnableAsync(user, command.Code);

        auditWriter.Record(AuditAction.TwoFactorEnabled, AuditEntityType.User, user.Id, user.UserName, new { via = "profile" });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesResult(codes);
    }
}

/// <summary>
/// Checks the code outside any transaction (a wrong one must keep counting toward the lockout, which a rollback would
/// undo), then turns two-factor off in the transaction of <see cref="ApplyTwoFactorDisableCommand"/>.
/// </summary>
public class DisableTwoFactorCommandHandler(
    UserManager<ApplicationUser> userManager,
    TwoFactorService twoFactor,
    ISender sender)
    : TwoFactorHandler(userManager), ICommandHandler<DisableTwoFactorCommand>
{
    public async Task<Unit> Handle(DisableTwoFactorCommand command, CancellationToken cancellationToken)
    {
        await twoFactor.VerifyForDisableAsync(await GetUserAsync(command.UserId), command.Code);

        await sender.Send(new ApplyTwoFactorDisableCommand(command.UserId), cancellationToken);
        return Unit.Value;
    }
}

internal class ApplyTwoFactorDisableCommandHandler(
    UserManager<ApplicationUser> userManager,
    TwoFactorService twoFactor,
    IAuthAuditWriter auditWriter)
    : TwoFactorHandler(userManager), ICommandHandler<ApplyTwoFactorDisableCommand>
{
    // The behavior owns SaveChanges + Commit, so this must not call SaveChangesAsync itself.
    public async Task<Unit> Handle(ApplyTwoFactorDisableCommand command, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(command.UserId);
        await twoFactor.ResetAsync(user);

        auditWriter.Record(AuditAction.TwoFactorDisabled, AuditEntityType.User, user.Id, user.UserName);
        return Unit.Value;
    }
}

public class RegenerateRecoveryCodesCommandHandler(
    UserManager<ApplicationUser> userManager,
    TwoFactorService twoFactor,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext)
    : TwoFactorHandler(userManager), ICommandHandler<RegenerateRecoveryCodesCommand, RecoveryCodesResult>
{
    public async Task<RecoveryCodesResult> Handle(RegenerateRecoveryCodesCommand command, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(command.UserId);
        var codes = await twoFactor.RegenerateRecoveryCodesAsync(user, command.Code);

        // The codes themselves are never logged.
        auditWriter.Record(AuditAction.TwoFactorRecoveryCodesRegenerated, AuditEntityType.User, user.Id, user.UserName);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesResult(codes);
    }
}

/// <summary>
/// "Set up a new device": the code (authenticator or recovery) is checked here, outside any transaction so a wrong one
/// still counts toward the lockout; then the account is reset exactly as an admin reset would, in its own transaction.
/// </summary>
public class NewTwoFactorDeviceCommandHandler(
    UserManager<ApplicationUser> userManager,
    TwoFactorService twoFactor,
    IAuthAuditWriter auditWriter,
    ISender sender)
    : TwoFactorHandler(userManager), ICommandHandler<NewTwoFactorDeviceCommand, ResetTwoFactorResult>
{
    public async Task<ResetTwoFactorResult> Handle(NewTwoFactorDeviceCommand command, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(command.UserId);

        // Written at once, before anything else can throw, and on the explicit token None: the code is spent now and that
        // cannot be undone, so a rolled-back reset must not leave it without a trace. (Identity's own calls still observe
        // the request's abort, so a client that disconnects here can still stop the reset; the user then signs in or
        // retries with another recovery code, and this row says which one was spent.)
        if (await twoFactor.VerifyForNewDeviceAsync(user, command.Code))
            await auditWriter.RecordAuthEventAsync(AuditAction.RecoveryCodeUsed, user.Id, user.UserName,
                new { via = "profile" }, CancellationToken.None); // no `left`: the reset takes every code away next
        await twoFactor.ClearFailedAttemptsAsync(user);

        // None here too, so the reset's own save and commit are not cancelled half way; the same limit applies to Identity.
        return await sender.Send(new ResetTwoFactorCommand(user.Id), CancellationToken.None);
    }
}

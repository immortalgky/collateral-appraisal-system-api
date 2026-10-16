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

public class DisableTwoFactorCommandHandler(
    UserManager<ApplicationUser> userManager,
    TwoFactorService twoFactor,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext)
    : TwoFactorHandler(userManager), ICommandHandler<DisableTwoFactorCommand>
{
    public async Task<Unit> Handle(DisableTwoFactorCommand command, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(command.UserId);
        await twoFactor.DisableAsync(user, command.Code);

        auditWriter.Record(AuditAction.TwoFactorDisabled, AuditEntityType.User, user.Id, user.UserName);
        await dbContext.SaveChangesAsync(cancellationToken);
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

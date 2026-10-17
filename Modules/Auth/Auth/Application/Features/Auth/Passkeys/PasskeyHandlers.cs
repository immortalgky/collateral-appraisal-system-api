using System.Text.Json;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Passkeys;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;
using Shared.Time;

namespace Auth.Application.Features.Auth.Passkeys;

public class GetMyPasskeysQueryHandler(AuthDbContext dbContext) : IQueryHandler<GetMyPasskeysQuery, IReadOnlyList<PasskeyDto>>
{
    public async Task<IReadOnlyList<PasskeyDto>> Handle(GetMyPasskeysQuery query, CancellationToken cancellationToken) =>
        await dbContext.UserPasskeys.AsNoTracking()
            .Where(p => p.UserId == query.UserId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(PasskeyDto.Projection)
            .ToListAsync(cancellationToken);
}

public class BeginPasskeyRegistrationCommandHandler(
    UserManager<ApplicationUser> userManager,
    PasskeyService passkeys,
    AuthDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<BeginPasskeyRegistrationCommand, PasskeyRegistrationOptionsResult>
{
    /// <summary>How recently the caller must have signed in to add a passkey: a stolen session cannot plant a lasting credential.</summary>
    public static readonly TimeSpan ReauthWindow = TimeSpan.FromMinutes(10);

    public async Task<PasskeyRegistrationOptionsResult> Handle(
        BeginPasskeyRegistrationCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
                   ?? throw new NotFoundException("User", command.UserId);

        // The session's CreatedAt is when this browser signed in. It does not move when the session renews its ticket or
        // records activity (unlike LastActivityAt), so a session kept alive for days never counts as a fresh sign-in.
        var signedInAt = command.SessionId is { } sessionId
            ? await dbContext.UserSessions.AsNoTracking()
                .Where(s => s.Id == sessionId && s.UserId == command.UserId && s.EndedAt == null)
                .Select(s => (DateTime?)s.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            : null;
        if (signedInAt is null || dateTimeProvider.ApplicationNow - signedInAt.Value > ReauthWindow)
            throw new BadRequestException(
                "Sign in again to add a passkey.", null, PasskeyErrors.ReauthRequired);

        var (options, state) = await passkeys.BeginRegistrationAsync(user);
        return new PasskeyRegistrationOptionsResult(JsonSerializer.Deserialize<JsonElement>(options), state);
    }
}

public class RegisterPasskeyCommandHandler(
    UserManager<ApplicationUser> userManager,
    PasskeyService passkeys,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext)
    : ICommandHandler<RegisterPasskeyCommand, PasskeyDto>
{
    public async Task<PasskeyDto> Handle(RegisterPasskeyCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
                   ?? throw new NotFoundException("User", command.UserId);

        var passkey = await passkeys.FinishRegistrationAsync(user, command.State, command.Name, command.Credential);

        auditWriter.Record(AuditAction.PasskeyAdded, AuditEntityType.User, user.Id, user.UserName,
            new { passkeyId = passkey.Id, name = passkey.Name, synced = passkey.IsBackedUp });
        try
        {
            // One save: the passkey and its audit row are written together or not at all.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (PasskeyService.IsCredentialIdConflict(ex))
        {
            // The same registration sent twice at once: the unique index let one through.
            throw PasskeyService.AlreadyRegistered();
        }

        return PasskeyDto.From(passkey);
    }
}

public class RenamePasskeyCommandHandler(AuthDbContext dbContext) : ICommandHandler<RenamePasskeyCommand>
{
    public async Task<Unit> Handle(RenamePasskeyCommand command, CancellationToken cancellationToken)
    {
        // Somebody else's passkey is "not found", not "forbidden": its existence is none of the caller's business.
        var passkey = await dbContext.UserPasskeys.FirstOrDefaultAsync(
                          p => p.Id == command.PasskeyId && p.UserId == command.UserId, cancellationToken)
                      ?? throw new NotFoundException("Passkey", command.PasskeyId);

        passkey.Name = PasskeyService.ValidName(command.Name);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public class DeletePasskeyCommandHandler(AuthDbContext dbContext, UserManager<ApplicationUser> userManager, IAuthAuditWriter auditWriter)
    : ICommandHandler<DeletePasskeyCommand>
{
    public async Task<Unit> Handle(DeletePasskeyCommand command, CancellationToken cancellationToken)
    {
        var passkey = await dbContext.UserPasskeys.FirstOrDefaultAsync(
                          p => p.Id == command.PasskeyId && p.UserId == command.UserId, cancellationToken)
                      ?? throw new NotFoundException("Passkey", command.PasskeyId);
        var userName = await userManager.Users.Where(u => u.Id == command.UserId).Select(u => u.UserName)
            .FirstOrDefaultAsync(cancellationToken);

        dbContext.UserPasskeys.Remove(passkey);
        auditWriter.Record(AuditAction.PasskeyRemoved, AuditEntityType.User, command.UserId, userName,
            new { passkeyId = passkey.Id, name = passkey.Name });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

using Auth.Application.Configurations;
using Auth.Application.Features.Users.ResetTwoFactor;
using Auth.Application.Services;

namespace Auth.Application.Features.Auth.TwoFactor;

public record BeginTwoFactorSetupCommand(Guid UserId) : ICommand<TwoFactorSetupInfo>;

/// <summary>
/// A command that carries a typed code, which may be an unspent recovery code. LoggingBehavior logs every request at
/// Information, so the one ToString here redacts it (see ChangePasswordCommand.ToString) and cannot be overridden.
/// </summary>
public abstract record TwoFactorCodeCommand(Guid UserId, string? Code)
{
    public sealed override string ToString() => $"{GetType().Name} {{ UserId = {UserId}, {nameof(Code)} = *** }}";
}

public record EnableTwoFactorCommand(Guid UserId, string? Code)
    : TwoFactorCodeCommand(UserId, Code), ICommand<RecoveryCodesResult>;

public record DisableTwoFactorCommand(Guid UserId, string? Code) : TwoFactorCodeCommand(UserId, Code), ICommand;

/// <summary>
/// The write half of /disable. ITransactionalCommand, like ResetTwoFactorCommand: turning two-factor off is several Identity
/// saves (the flag, the recovery codes, the key, the security stamp) plus the audit row, and a refusal part-way must not
/// leave it half done. Regenerate and Enable need nothing like it: each ends in a single Identity save, and Enable writes
/// its codes before switching on, so a failure never leaves two-factor on without them.
/// </summary>
/// Internal: only ever sent by DisableTwoFactorCommandHandler, after VerifyForDisableAsync.
internal record ApplyTwoFactorDisableCommand(Guid UserId) : ICommand, ITransactionalCommand<IAuthUnitOfWork>;

public record RegenerateRecoveryCodesCommand(Guid UserId, string? Code)
    : TwoFactorCodeCommand(UserId, Code), ICommand<RecoveryCodesResult>;

/// <summary>Not transactional itself: a wrong code must keep counting toward the lockout, which a rollback would undo.
/// The reset it ends with is the admin one, which is.</summary>
public record NewTwoFactorDeviceCommand(Guid UserId, string? Code)
    : TwoFactorCodeCommand(UserId, Code), ICommand<ResetTwoFactorResult>;

/// <summary>The one-time recovery codes. Shown once and never readable again.</summary>
public record RecoveryCodesResult(IReadOnlyList<string> RecoveryCodes);

/// <summary>Body of the endpoints that need a current authenticator code.</summary>
public record TwoFactorCodeRequest(string? Code);

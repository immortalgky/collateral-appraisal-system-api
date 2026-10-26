namespace Auth.Domain.Auditing;

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
    AssignmentChanged,
    LoggedIn,
    LoginFailed,
    LoggedOut,
    SessionStarted,
    SessionEnded,
    TwoFactorEnabled,
    TwoFactorDisabled,
    TwoFactorReset,
    RecoveryCodeUsed,
    TwoFactorRecoveryCodesRegenerated,
    PasskeyAdded,
    PasskeyRemoved,
    PasskeysRevoked
}

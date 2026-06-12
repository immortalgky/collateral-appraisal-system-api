namespace Auth.Domain.Auditing;

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
    AssignmentChanged,
    LoggedIn,
    LoginFailed,
    LoggedOut
}

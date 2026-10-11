namespace Auth.Domain.Sessions;

/// <summary>
/// Why a browser session ended. Stored as a string so the audit trail and the table stay readable.
/// </summary>
public enum SessionEndReason
{
    /// <summary>The user signed out.</summary>
    Logout,

    /// <summary>No real (non-background) activity for the idle timeout.</summary>
    Idle,

    /// <summary>The same browser started a new sign-in (<c>/auth/login</c>).</summary>
    Replaced,

    /// <summary>Ended by an administrator or because the account no longer exists.</summary>
    Revoked,

    /// <summary>The account was deactivated.</summary>
    Deactivated,

    /// <summary>An administrator reset the password, or opened a new access window (which rotates it).</summary>
    PasswordReset,

    /// <summary>The password was changed. Sessions other than the one that changed it end.</summary>
    PasswordChanged,

    /// <summary>The temporary-access window closed.</summary>
    AccessExpired
}

/// <summary>
/// One signed-in browser. The app cookie carries only this row's id; the authentication ticket lives
/// here, so ending the row ends the session regardless of what the browser still holds.
/// </summary>
public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The authentication ticket, serialized and protected with Data Protection.</summary>
    public byte[] Ticket { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    /// <summary>Last real (non-background) request. The idle timeout is measured from here.</summary>
    public DateTime LastActivityAt { get; set; }

    public DateTime? EndedAt { get; set; }
    public SessionEndReason? EndReason { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}

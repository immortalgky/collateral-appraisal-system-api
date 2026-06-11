namespace Auth.Domain.Identity;

/// <summary>
/// A previously-used password hash for a user, kept so the policy can forbid reuse of the last
/// N passwords. One row is appended on every password set (register / change / reset); older
/// rows beyond the configured retention are trimmed.
/// </summary>
public class PasswordHistory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

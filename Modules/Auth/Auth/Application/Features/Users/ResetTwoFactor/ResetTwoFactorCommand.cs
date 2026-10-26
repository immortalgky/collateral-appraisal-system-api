using Auth.Application.Configurations;

namespace Auth.Application.Features.Users.ResetTwoFactor;

// ITransactionalCommand: UserManager writes through the request's AuthDbContext, so the key, the flag, the recovery codes,
// the revoked sessions and the audit row commit or roll back together. A failure part-way must never leave two-factor off
// while the user's sessions are still alive. The behavior owns SaveChanges + Commit, so the handler must NOT call
// SaveChangesAsync itself.
public record ResetTwoFactorCommand(Guid UserId)
    : ICommand<ResetTwoFactorResult>, ITransactionalCommand<IAuthUnitOfWork>;

/// <summary>The sessions this ended, for the endpoint to evict from the cache once the transaction has committed.</summary>
public record ResetTwoFactorResult(IReadOnlyList<Guid> EndedSessionIds);

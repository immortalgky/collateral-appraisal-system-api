using Auth.Application.Configurations;

namespace Auth.Application.Features.Users.RevokePasskeys;

// ITransactionalCommand, as for Reset 2FA: the passkeys, the ended sessions and the audit row commit or roll back together,
// so a failure part-way never leaves the passkeys gone with the sessions still alive. The behavior owns SaveChanges +
// Commit, so the handler must NOT call SaveChangesAsync itself.
public record RevokePasskeysCommand(Guid UserId)
    : ICommand<RevokePasskeysResult>, ITransactionalCommand<IAuthUnitOfWork>;

/// <summary>The sessions this ended, for the endpoint to evict from the cache once the transaction has committed.</summary>
public record RevokePasskeysResult(IReadOnlyList<Guid> EndedSessionIds);

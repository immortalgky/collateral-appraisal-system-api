using Auth.Application.Configurations;

namespace Auth.Application.Features.Users.SetAccessWindow;

/// <summary>
/// Opens, extends or closes the access window on a temporary-access account.
/// <para>
/// Opening rotates the account's password (the caller gets it back once). Extending deliberately does
/// not, so whoever is mid-job keeps working. Closing is "set an <see cref="ExpiresAt"/> that has
/// already passed" — it rotates the password too, so no usable secret is left behind.
/// </para>
/// </summary>
// ITransactionalCommand wraps the handler in one transaction (see TransactionalBehavior). The
// password reset commits through the Identity user store the moment it runs, so without this a
// failure while writing the audit row would leave a window open, its password rotated beyond reach
// of the admin who asked for it, and no record of who opened it. The behavior owns SaveChanges +
// Commit, so the handler must NOT call SaveChangesAsync itself.
public record SetAccessWindowCommand(Guid UserId, DateTime ExpiresAt, string Reason, bool ExtendOnly)
    : ICommand<SetAccessWindowResult>, ITransactionalCommand<IAuthUnitOfWork>;

/// <param name="Password">The newly generated password — null when nothing was rotated (an extend, or
/// a close, where a password would be useless anyway). Shown to the admin once and never again.</param>
public record SetAccessWindowResult(DateTime AccessExpiresAt, string? Password);

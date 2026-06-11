using Notification.Domain.Email;

namespace Notification.Infrastructure.Email;

/// <summary>
/// Writes an <see cref="EmailSendLog"/> row to the database. Callers always wrap
/// invocations in a best-effort try/catch so a persistence failure never masks the
/// real email send result.
/// </summary>
internal interface IEmailSendLogWriter
{
    Task WriteAsync(EmailSendLog log, CancellationToken ct = default);
}

using Notification.Data;
using Notification.Domain.Email;

namespace Notification.Infrastructure.Email;

/// <summary>
/// Persists an <see cref="EmailSendLog"/> row inside <see cref="NotificationDbContext"/>.
/// Each call opens its own <see cref="SaveChangesAsync"/> so it is independent of any
/// surrounding unit of work. The timestamp is set by the caller (<see cref="SmtpEmailSender"/>).
/// </summary>
internal sealed class EmailSendLogWriter(
    NotificationDbContext dbContext) : IEmailSendLogWriter
{
    public async Task WriteAsync(EmailSendLog log, CancellationToken ct = default)
    {
        dbContext.EmailSendLogs.Add(log);
        await dbContext.SaveChangesAsync(ct);
    }
}

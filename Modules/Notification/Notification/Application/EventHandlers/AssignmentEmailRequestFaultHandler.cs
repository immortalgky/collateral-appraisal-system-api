using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Filters;

namespace Notification.Application.EventHandlers;

/// <summary>
/// The assignment email gave up (#624): MassTransit publishes this Fault once retries are exhausted, or at once for a
/// permanent SMTP rejection (<see cref="Notification.Infrastructure.Email.SmtpFailures"/>).
/// </summary>
public sealed class AssignmentEmailRequestFaultHandler(
    AssignmentEmailFailureNotifier notifier,
    InboxGuard<NotificationDbContext> inboxGuard)
    : IConsumer<Fault<AssignmentEmailRequestedIntegrationEvent>>
{
    public Task Consume(ConsumeContext<Fault<AssignmentEmailRequestedIntegrationEvent>> context) =>
        // Keyed on the request, not the fault: a redelivered request that faults again publishes a new fault message.
        inboxGuard.RunOnceAsync(context.Message.FaultedMessageId ?? context.MessageId, GetType().Name, ct =>
            notifier.NotifyAsync(context, context.Message.Message,
                Reason(context.Message.Exceptions.FirstOrDefault()), ct), context.CancellationToken);

    /// <summary>
    /// The mail libraries' own message (the gateway's reply, a bad address) helps the admin. A relay that cannot be
    /// reached (socket error, or MailKit's connect/read timeout) gets a fixed text, because .NET's connect failure names
    /// the relay's address and port. Attachment errors never fault (EmailAttachmentAssembler skips them); anything else,
    /// such as a SQL error, would show internals, so it stays in the logs and Failed Messages.
    /// </summary>
    private static string Reason(ExceptionInfo? exception)
    {
        if (exception?.ExceptionType is { } type
            && (type.StartsWith("MailKit.", StringComparison.Ordinal) || type.StartsWith("MimeKit.", StringComparison.Ordinal)))
            return exception.Message;

        // With STARTTLS, SslStream wraps the socket error or timeout in an IOException: look through the chain.
        for (var e = exception; e is not null; e = e.InnerException)
            if (e.ExceptionType is "System.Net.Sockets.SocketException" or "System.TimeoutException"
                || e.ExceptionType?.EndsWith("+ExtendedSocketException", StringComparison.Ordinal) == true)
                return "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้";

        return "ระบบส่งอีเมลขัดข้อง";
    }
}

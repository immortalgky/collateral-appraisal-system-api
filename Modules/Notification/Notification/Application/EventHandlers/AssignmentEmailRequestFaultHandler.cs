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
    /// What the admins are told:
    /// <list type="bullet">
    ///   <item>"cannot reach the mail server" for a socket error or timeout, on its own or under the transport's
    ///   wrappers (IOException from SslStream with STARTTLS, MailKit's SslHandshakeException). A fixed text, because
    ///   .NET's connect failure names the relay's address and port.</item>
    ///   <item>MailKit/MimeKit's own message otherwise (the gateway's reply, a bad address).</item>
    ///   <item>A generic text for anything else, such as a SQL or Redis error, even when a socket error sits inside
    ///   it: it would show internals or blame the relay wrongly. Attachment errors never fault
    ///   (EmailAttachmentAssembler skips them). The details stay in the logs and Failed Messages.</item>
    /// </list>
    /// </summary>
    private static string Reason(ExceptionInfo? exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e.ExceptionType is "System.Net.Sockets.SocketException" or "System.TimeoutException"
                || e.ExceptionType?.EndsWith("+ExtendedSocketException", StringComparison.Ordinal) == true)
                return "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้";
            if (e.ExceptionType is not ("System.IO.IOException" or "MailKit.Security.SslHandshakeException"))
                break;
        }

        return exception?.ExceptionType is { } type
               && (type.StartsWith("MailKit.", StringComparison.Ordinal) || type.StartsWith("MimeKit.", StringComparison.Ordinal))
            ? exception.Message
            : "ระบบส่งอีเมลขัดข้อง";
    }
}

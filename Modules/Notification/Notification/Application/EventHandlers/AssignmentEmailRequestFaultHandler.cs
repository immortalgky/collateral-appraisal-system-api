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
    /// What the admins are told. Fixed texts wherever .NET or MailKit would name the relay's address, port or
    /// certificate:
    /// <list type="bullet">
    ///   <item>"cannot reach the mail server": a socket error, a timeout or a dropped stream (IOException, which is also
    ///   how SslStream wraps the first two under STARTTLS).</item>
    ///   <item>"TLS failed": MailKit's SslHandshakeException, unless its cause is one of the above.</item>
    ///   <item>MailKit/MimeKit's own message otherwise (the gateway's reply, a bad address, a protocol error).</item>
    ///   <item>A generic text for anything else, such as a SQL or Redis error, even with a socket error inside it.
    ///   Attachment errors never fault (EmailAttachmentAssembler skips them). The details stay in the logs and Failed
    ///   Messages.</item>
    /// </list>
    /// </summary>
    private static string Reason(ExceptionInfo? exception)
    {
        const string unreachable = "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้";
        var type = exception?.ExceptionType;

        if (IsUnreachable(type)) return unreachable;
        if (type == "MailKit.Security.SslHandshakeException")
            return IsUnreachable(exception!.InnerException?.ExceptionType)
                ? unreachable
                : "เชื่อมต่อแบบเข้ารหัส (TLS) กับเซิร์ฟเวอร์อีเมลไม่สำเร็จ";
        if (type is not null
            && (type.StartsWith("MailKit.", StringComparison.Ordinal) || type.StartsWith("MimeKit.", StringComparison.Ordinal)))
            return exception!.Message;
        return "ระบบส่งอีเมลขัดข้อง";
    }

    private static bool IsUnreachable(string? type) =>
        type is "System.Net.Sockets.SocketException" or "System.TimeoutException" or "System.IO.IOException"
        || type?.EndsWith("+ExtendedSocketException", StringComparison.Ordinal) == true;
}

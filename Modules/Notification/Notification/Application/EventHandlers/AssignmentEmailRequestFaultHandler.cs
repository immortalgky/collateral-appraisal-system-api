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
    /// The mail libraries' own message (the gateway's reply, a bad address) helps the admin. A gateway that cannot be
    /// reached gets a fixed text: .NET's connect failure names the relay's address and port. Anything else, such as a
    /// SQL error or a timeout while attaching documents, would show internals or blame the gateway wrongly, so it
    /// stays in the logs and Failed Messages.
    /// </summary>
    private static string Reason(ExceptionInfo? exception) => exception?.ExceptionType switch
    {
        { } type when type.StartsWith("MailKit.", StringComparison.Ordinal)
                      || type.StartsWith("MimeKit.", StringComparison.Ordinal) => exception.Message,
        { } type when type.EndsWith("SocketException", StringComparison.Ordinal) => "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้",
        _ => "ระบบส่งอีเมลขัดข้อง"
    };
}

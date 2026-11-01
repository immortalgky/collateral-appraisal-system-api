using MailKit.Net.Smtp;

namespace Notification.Infrastructure.Email;

/// <summary>
/// A 5xx reply is the gateway's final answer (message too large, recipient refused): sending the same mail again gets
/// the same reply, so the bus retry policy ignores it (Program.cs) and the consumer faults at once (#624).
/// 4xx replies and connection errors stay retryable.
/// </summary>
public static class SmtpFailures
{
    public static bool IsPermanent(SmtpCommandException ex) => (int)ex.StatusCode is >= 500 and < 600;
}

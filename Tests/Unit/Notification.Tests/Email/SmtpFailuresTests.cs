using MailKit.Net.Smtp;
using Notification.Infrastructure.Email;

namespace Notification.Tests.Email;

/// <summary>#624: a permanent SMTP rejection is not retried (the bus policy ignores it), a transient one is.</summary>
public class SmtpFailuresTests
{
    [Theory]
    [InlineData(SmtpStatusCode.ExceededStorageAllocation, true)] // 552 message size
    [InlineData(SmtpStatusCode.MailboxUnavailable, true)] // 550
    [InlineData(SmtpStatusCode.ErrorInProcessing, false)] // 451
    [InlineData(SmtpStatusCode.ServiceNotAvailable, false)] // 421
    public void Only_a_5xx_reply_is_permanent(SmtpStatusCode status, bool permanent) =>
        Assert.Equal(permanent, SmtpFailures.IsPermanent(
            new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, status, "rejected")));
}

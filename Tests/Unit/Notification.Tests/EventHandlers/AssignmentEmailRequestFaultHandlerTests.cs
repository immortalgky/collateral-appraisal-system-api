using Auth.Contracts.Users;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Application.EventHandlers;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Notification.Tests.EventHandlers;

/// <summary>#624 Notification seam: the send gave up (Fault) → the row is reported Failed and the admins are told.</summary>
public class AssignmentEmailRequestFaultHandlerTests
{
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IUserLookupService _users = Substitute.For<IUserLookupService>();
    private readonly ConsumeContext<Fault<AssignmentEmailRequestedIntegrationEvent>> _ctx =
        Substitute.For<ConsumeContext<Fault<AssignmentEmailRequestedIntegrationEvent>>>();

    private static readonly AssignmentEmailRequestedIntegrationEvent Request = new()
    {
        AppraisalId = Guid.NewGuid(),
        EmailId = Guid.NewGuid(),
        To = ["jobs@acme.co.th"],
        Subject = "ขอนำส่งงานประเมิน",
        HtmlBody = "<p>x</p>",
        AssignedBy = "P5229",
        AppraisalNumber = "69042630",
        CompanyName = "บริษัท แอคมี จำกัด"
    };

    public AssignmentEmailRequestFaultHandlerTests()
    {
        _users.GetUsernamesInGroupAsync("IntAdmin", Arg.Any<CancellationToken>())
            .Returns(["P1001", "P5229"]); // the assigner is an IntAdmin too
        _users.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IEnumerable<string>>()
                .Where(u => u.Equals("P5229", StringComparison.OrdinalIgnoreCase) || u.Equals("P7777", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(u => u, u => new UserLookupDto(u.ToUpperInvariant(), "สมชาย", "ใจดี"),
                    StringComparer.OrdinalIgnoreCase) as IReadOnlyDictionary<string, UserLookupDto>);
        _ctx.MessageId.Returns((Guid?)null); // the real InboxGuard below never touches its provider-less context
        _ctx.CancellationToken.Returns(CancellationToken.None);
    }

    private Task ConsumeAsync(
        AssignmentEmailRequestedIntegrationEvent request, string error,
        string exceptionType = "MailKit.Net.Smtp.SmtpCommandException", ExceptionInfo? inner = null)
    {
        var exception = Info(exceptionType, error, inner);
        var fault = Substitute.For<Fault<AssignmentEmailRequestedIntegrationEvent>>();
        fault.Message.Returns(request);
        fault.Exceptions.Returns([exception]);
        _ctx.Message.Returns(fault);

        var inbox = new InboxGuard<NotificationDbContext>(
            new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().Options),
            NullLogger<InboxGuard<NotificationDbContext>>.Instance,
            Substitute.For<IDateTimeProvider>());
        var notifier = new AssignmentEmailFailureNotifier(
            _notifications, _users, NullLogger<AssignmentEmailFailureNotifier>.Instance);
        return new AssignmentEmailRequestFaultHandler(notifier, inbox).Consume(_ctx);
    }

    [Fact]
    public async Task A_send_that_gave_up_marks_the_email_Failed_and_tells_each_admin_once()
    {
        await ConsumeAsync(Request, "5.3.4 Message size exceeds fixed maximum message size");

        await _ctx.Received(1).Publish(Arg.Is<AssignmentEmailFailedIntegrationEvent>(e =>
                e.AppraisalId == Request.AppraisalId
                && e.EmailId == Request.EmailId
                && e.Reason == "5.3.4 Message size exceeds fixed maximum message size"),
            Arg.Any<CancellationToken>());

        const string expected =
            "ส่งอีเมลแจ้งงาน 69042630 ถึง บริษัท แอคมี จำกัด ไม่สำเร็จ: 5.3.4 Message size exceeds fixed maximum message size";
        foreach (var user in new[] { "P5229", "P1001" })
            await _notifications.Received(1).SendNotificationToUserAsync(
                user, Arg.Any<string>(), expected, NotificationType.AssignmentEmailFailed,
                $"/appraisals/{Request.AppraisalId}/administration", Arg.Any<Dictionary<string, object>?>());
        await _notifications.ReceivedWithAnyArgs(2).SendNotificationToUserAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task The_admins_come_from_the_IntAdmin_group_not_the_role()
    {
        _users.GetUsernamesInRoleAsync("IntAdmin", Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(["ROLEONLY"]);

        await ConsumeAsync(Request, "connection refused");

        await _users.Received(1).GetUsernamesInGroupAsync("IntAdmin", Arg.Any<CancellationToken>());
        await _users.DidNotReceiveWithAnyArgs().GetUsernamesInRoleAsync(default!, default, default);
        await _notifications.DidNotReceive().SendNotificationToUserAsync(
            "ROLEONLY", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(),
            Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>());
    }

    [Fact]
    public async Task A_failure_outside_the_mail_gateway_is_reported_without_its_internals()
    {
        // e.g. a SQL error (the inbox claim): admins see a generic reason, the details stay in the logs.
        await ConsumeAsync(Request, "Invalid object name 'document.Documents'. Server db01",
            "Microsoft.Data.SqlClient.SqlException");

        await _ctx.Received(1).Publish(
            Arg.Is<AssignmentEmailFailedIntegrationEvent>(e => e.Reason == "ระบบส่งอีเมลขัดข้อง"),
            Arg.Any<CancellationToken>());
        await _notifications.Received().SendNotificationToUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Is<string>(m => m.EndsWith("ไม่สำเร็จ: ระบบส่งอีเมลขัดข้อง")),
            NotificationType.AssignmentEmailFailed, Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>());
    }

    [Fact]
    public async Task A_gateway_that_cannot_be_reached_is_reported_as_such_without_its_address()
    {
        // .NET's connect failure is a SocketException subclass whose message names the relay's endpoint.
        await ConsumeAsync(Request, "Connection refused 10.20.30.40:25",
            "System.Net.Internals.SocketExceptionFactory+ExtendedSocketException");

        await _ctx.Received(1).Publish(
            Arg.Is<AssignmentEmailFailedIntegrationEvent>(e => e.Reason == "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_relay_that_never_answers_is_reported_as_unreachable()
    {
        // A firewall dropping packets: MailKit's connect/read timeout. Attachment errors never get here (the
        // assembler skips them), so a timeout is the gateway's.
        await ConsumeAsync(Request, "The operation has timed out.", "System.TimeoutException");

        await _ctx.Received(1).Publish(
            Arg.Is<AssignmentEmailFailedIntegrationEvent>(e => e.Reason == "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้"),
            Arg.Any<CancellationToken>());
    }

    private static ExceptionInfo Info(string type, string message, ExceptionInfo? inner = null)
    {
        var info = Substitute.For<ExceptionInfo>();
        info.ExceptionType.Returns(type);
        info.Message.Returns(message);
        info.InnerException.Returns(inner); // never an auto-substitute: the chain must end
        return info;
    }

    [Theory]
    // STARTTLS: SslStream wraps the timeout or socket error in an IOException.
    [InlineData("System.IO.IOException", "System.TimeoutException", "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้")]
    [InlineData("System.IO.IOException", "System.Net.Internals.SocketExceptionFactory+ExtendedSocketException",
        "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้")]
    // The relay stalls during the TLS handshake: MailKit wraps it.
    [InlineData("MailKit.Security.SslHandshakeException", "System.TimeoutException", "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้")]
    // The database or Redis is down, not the relay.
    [InlineData("Microsoft.Data.SqlClient.SqlException", "System.Net.Sockets.SocketException", "ระบบส่งอีเมลขัดข้อง")]
    [InlineData("StackExchange.Redis.RedisConnectionException", "System.Net.Sockets.SocketException", "ระบบส่งอีเมลขัดข้อง")]
    // A certificate problem: MailKit's message names the relay's host and the certificate's name.
    [InlineData("MailKit.Security.SslHandshakeException", "System.Security.Authentication.AuthenticationException",
        "เชื่อมต่อแบบเข้ารหัส (TLS) กับเซิร์ฟเวอร์อีเมลไม่สำเร็จ")]
    public async Task Only_a_transport_wrapper_is_looked_through(string outer, string inner, string reason)
    {
        await ConsumeAsync(Request, "wrapped 10.20.30.40:25", outer, Info(inner, "inner 10.20.30.40:25"));

        await _ctx.Received(1).Publish(
            Arg.Is<AssignmentEmailFailedIntegrationEvent>(e => e.Reason == reason), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_relay_that_drops_the_connection_is_reported_as_unreachable()
    {
        await ConsumeAsync(Request, "Received an unexpected EOF or 0 bytes from the transport stream.", "System.IO.IOException");

        await _ctx.Received(1).Publish(
            Arg.Is<AssignmentEmailFailedIntegrationEvent>(e => e.Reason == "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_mail_protocol_error_keeps_its_own_message_even_over_a_socket_error()
    {
        await ConsumeAsync(Request, "The SMTP server has unexpectedly disconnected.", "MailKit.Net.Smtp.SmtpProtocolException",
            Info("System.IO.IOException", "x", Info("System.Net.Sockets.SocketException", "Connection reset 10.20.30.40:25")));

        await _ctx.Received(1).Publish(
            Arg.Is<AssignmentEmailFailedIntegrationEvent>(e => e.Reason == "The SMTP server has unexpectedly disconnected."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_web_socket_error_is_not_taken_for_the_mail_relay()
    {
        await ConsumeAsync(Request, "The remote party closed the WebSocket", "System.Net.WebSockets.WebSocketException");

        await _ctx.Received(1).Publish(
            Arg.Is<AssignmentEmailFailedIntegrationEvent>(e => e.Reason == "ระบบส่งอีเมลขัดข้อง"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_assigner_who_is_not_a_user_is_not_notified()
    {
        // Automatic paths stamp "System" as the assigner.
        await ConsumeAsync(Request with { AssignedBy = "System" }, "connection refused");

        await _notifications.DidNotReceive().SendNotificationToUserAsync(
            "System", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(),
            Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>());
        await _notifications.ReceivedWithAnyArgs(2).SendNotificationToUserAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task The_assigner_is_notified_under_their_account_spelling()
    {
        // The realtime group is user-{Username} and case-sensitive: use the account's own spelling.
        _users.GetUsernamesInGroupAsync("IntAdmin", Arg.Any<CancellationToken>()).Returns(["P1001"]);

        await ConsumeAsync(Request with { AssignedBy = "p7777" }, "connection refused");

        await _notifications.Received(1).SendNotificationToUserAsync(
            "P7777", Arg.Any<string>(), Arg.Any<string>(), NotificationType.AssignmentEmailFailed,
            Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>());
        await _notifications.ReceivedWithAnyArgs(2).SendNotificationToUserAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task A_system_assigned_case_tells_every_IntAdmin()
    {
        _users.GetUsernamesInGroupAsync("IntAdmin", Arg.Any<CancellationToken>()).Returns(["P1001", "P1002"]);

        await ConsumeAsync(Request with { AssignedBy = null }, "connection refused");

        await _notifications.Received(1).SendNotificationToUserAsync(
            "P1001", Arg.Any<string>(), Arg.Any<string>(), NotificationType.AssignmentEmailFailed,
            Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>());
        await _notifications.Received(1).SendNotificationToUserAsync(
            "P1002", Arg.Any<string>(), Arg.Any<string>(), NotificationType.AssignmentEmailFailed,
            Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>());
        await _notifications.ReceivedWithAnyArgs(2).SendNotificationToUserAsync(default!, default!, default!, default);
    }
}

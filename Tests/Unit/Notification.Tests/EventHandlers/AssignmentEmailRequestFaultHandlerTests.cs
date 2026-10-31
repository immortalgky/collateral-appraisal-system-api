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
        _users.GetUsernamesInRoleAsync("IntAdmin", null, Arg.Any<CancellationToken>())
            .Returns(["P1001", "P5229"]); // the assigner is an IntAdmin too
        _ctx.MessageId.Returns((Guid?)null); // the real InboxGuard below never touches its provider-less context
        _ctx.CancellationToken.Returns(CancellationToken.None);
    }

    private Task ConsumeAsync(AssignmentEmailRequestedIntegrationEvent request, string error)
    {
        var exception = Substitute.For<ExceptionInfo>();
        exception.Message.Returns(error);
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
    public async Task A_system_assigned_case_tells_every_IntAdmin()
    {
        _users.GetUsernamesInRoleAsync("IntAdmin", null, Arg.Any<CancellationToken>()).Returns(["P1001", "P1002"]);

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

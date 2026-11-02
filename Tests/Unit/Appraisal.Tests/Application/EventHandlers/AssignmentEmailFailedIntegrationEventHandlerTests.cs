using Appraisal.Application.EventHandlers;
using Appraisal.Domain.Appraisals;
using MassTransit;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Tests.Application.EventHandlers;

/// <summary>#624: Notification reports an assignment email it could not send.</summary>
public class AssignmentEmailFailedIntegrationEventHandlerTests
{
    private readonly IAssignmentEmailRepository _emails = Substitute.For<IAssignmentEmailRepository>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    [Fact]
    public async Task The_report_is_one_conditional_update_stamped_on_arrival()
    {
        var now = new DateTime(2026, 11, 2, 10, 30, 0);
        _clock.ApplicationNow.Returns(now);
        var emailId = Guid.NewGuid();
        var ctx = Substitute.For<ConsumeContext<AssignmentEmailFailedIntegrationEvent>>();
        ctx.Message.Returns(new AssignmentEmailFailedIntegrationEvent { AppraisalId = Guid.NewGuid(), EmailId = emailId, Reason = "refused" });
        ctx.CancellationToken.Returns(CancellationToken.None);

        await new AssignmentEmailFailedIntegrationEventHandler(_emails, _clock).Consume(ctx);

        await _emails.Received(1).MarkFailedIfQueuedAsync(emailId, "refused", now, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, "ไม่ทราบสาเหตุ")]
    [InlineData("  ", "ไม่ทราบสาเหตุ")]
    [InlineData(" refused ", "refused")]
    public void The_reason_is_trimmed_and_never_blank(string? reason, string expected) =>
        Assert.Equal(expected, AssignmentEmail.NormalizeFailureReason(reason));

    [Fact]
    public void A_long_reason_is_cut_to_the_column() =>
        Assert.Equal(AssignmentEmail.MaxFailureReasonLength,
            AssignmentEmail.NormalizeFailureReason(new string('x', AssignmentEmail.MaxFailureReasonLength + 50)).Length);

    [Fact]
    public void Only_a_queued_email_becomes_failed()
    {
        var email = AssignmentEmail.ToSend(Guid.NewGuid(), Guid.NewGuid(), [], [], "subject", "<p>body</p>", []);
        email.Queue("บริษัท แอคมี จำกัด", "สมชาย ใจดี", ["info@acme.co.th"], []);
        email.MarkSent(new DateTime(2026, 11, 1, 10, 30, 0));

        email.MarkFailed("connection refused");

        Assert.Equal(AssignmentEmailStatus.Sent, email.Status);
        Assert.Null(email.FailureReason);
    }

    [Fact]
    public void A_failed_email_that_a_redrive_delivered_becomes_sent()
    {
        // Ops redrove the faulted request from Failed Messages and the gateway took it this time.
        var email = AssignmentEmail.ToSend(Guid.NewGuid(), Guid.NewGuid(), [], [], "subject", "<p>body</p>", []);
        email.Queue("บริษัท แอคมี จำกัด", "สมชาย ใจดี", ["info@acme.co.th"], []);
        email.MarkFailed("connection refused");

        email.MarkSent(new DateTime(2026, 11, 1, 10, 30, 0));

        Assert.Equal(AssignmentEmailStatus.Sent, email.Status);
        Assert.Null(email.FailureReason);
    }
}

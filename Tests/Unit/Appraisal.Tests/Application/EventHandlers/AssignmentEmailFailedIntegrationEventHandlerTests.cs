using Appraisal.Application.EventHandlers;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using NSubstitute;
using Shared.Messaging.Events;

namespace Appraisal.Tests.Application.EventHandlers;

/// <summary>#624: Notification reports an assignment email it could not send.</summary>
public class AssignmentEmailFailedIntegrationEventHandlerTests
{
    private readonly IAssignmentEmailRepository _emails = Substitute.For<IAssignmentEmailRepository>();
    private readonly IAppraisalUnitOfWork _uow = Substitute.For<IAppraisalUnitOfWork>();

    private Task ReportAsync(AssignmentEmail email, string reason)
    {
        _emails.GetByIdAsync(email.Id, Arg.Any<CancellationToken>()).Returns(email);
        var ctx = Substitute.For<ConsumeContext<AssignmentEmailFailedIntegrationEvent>>();
        ctx.Message.Returns(new AssignmentEmailFailedIntegrationEvent
        {
            AppraisalId = email.AppraisalId, EmailId = email.Id, Reason = reason
        });
        ctx.CancellationToken.Returns(CancellationToken.None);
        return new AssignmentEmailFailedIntegrationEventHandler(_emails, _uow).Consume(ctx);
    }

    private static AssignmentEmail Queued()
    {
        var email = AssignmentEmail.ToSend(Guid.NewGuid(), Guid.NewGuid(), [], [], "subject", "<p>body</p>", []);
        email.Queue("บริษัท แอคมี จำกัด", "สมชาย ใจดี");
        return email;
    }

    [Fact]
    public async Task A_queued_email_becomes_failed_with_the_reason()
    {
        var email = Queued();

        await ReportAsync(email, "5.3.4 Message size exceeds fixed maximum message size");

        Assert.Equal(AssignmentEmailStatus.Failed, email.Status);
        Assert.Equal("5.3.4 Message size exceeds fixed maximum message size", email.FailureReason);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_sent_email_stays_sent()
    {
        var email = Queued();
        email.MarkSent();

        await ReportAsync(email, "connection refused");

        Assert.Equal(AssignmentEmailStatus.Sent, email.Status);
        Assert.Null(email.FailureReason);
    }

    [Fact]
    public async Task A_long_reason_is_cut_to_the_column()
    {
        var email = Queued();

        await ReportAsync(email, new string('x', 250));

        Assert.Equal(200, email.FailureReason!.Length);
    }

    [Fact]
    public void A_failed_email_that_a_redrive_delivered_becomes_sent()
    {
        // Ops redrove the faulted request from Failed Messages and the gateway took it this time.
        var email = Queued();
        email.MarkFailed("connection refused");

        email.MarkSent();

        Assert.Equal(AssignmentEmailStatus.Sent, email.Status);
        Assert.Null(email.FailureReason);
    }
}

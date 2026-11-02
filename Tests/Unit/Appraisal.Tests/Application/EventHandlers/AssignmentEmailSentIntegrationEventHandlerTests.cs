using Appraisal.Application.EventHandlers;
using Appraisal.Domain.Appraisals;
using MassTransit;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Tests.Application.EventHandlers;

/// <summary>#620: Notification's confirmation closes the round trip of an assignment email.</summary>
public class AssignmentEmailSentIntegrationEventHandlerTests
{
    private readonly IAssignmentEmailRepository _emails = Substitute.For<IAssignmentEmailRepository>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    [Fact]
    public async Task The_confirmation_is_one_conditional_update_stamped_on_arrival()
    {
        var now = new DateTime(2026, 11, 2, 10, 30, 0);
        _clock.ApplicationNow.Returns(now);
        var emailId = Guid.NewGuid();
        var ctx = Substitute.For<ConsumeContext<AssignmentEmailSentIntegrationEvent>>();
        ctx.Message.Returns(new AssignmentEmailSentIntegrationEvent { AppraisalId = Guid.NewGuid(), EmailId = emailId });
        ctx.CancellationToken.Returns(CancellationToken.None);

        await new AssignmentEmailSentIntegrationEventHandler(_emails, _clock).Consume(ctx);

        await _emails.Received(1).MarkSentIfQueuedOrFailedAsync(emailId, now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void An_address_already_in_to_is_not_stored_as_copied()
    {
        var email = AssignmentEmail.ToSend(Guid.NewGuid(), Guid.NewGuid(), [], [], "subject", "<p>body</p>", []);

        email.Queue("บริษัท แอคมี จำกัด", "สมชาย ใจดี", ["jobs@acme.co.th"], ["JOBS@acme.co.th", "boss@bank.co.th", "Boss@bank.co.th"]);

        Assert.Equal(["boss@bank.co.th"], email.SentCc); // as the sender mails it (Except also drops duplicates)
    }

    [Fact]
    public void A_pending_email_cannot_be_confirmed_by_the_domain_method()
    {
        var email = AssignmentEmail.ToSend(Guid.NewGuid(), Guid.NewGuid(), [], [], "subject", "<p>body</p>", []);

        email.MarkSent(DateTime.Now);

        Assert.Equal(AssignmentEmailStatus.Pending, email.Status);
    }
}

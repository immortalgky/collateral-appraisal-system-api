using Appraisal.Application.EventHandlers;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using NSubstitute;
using Shared.Messaging.Events;

namespace Appraisal.Tests.Application.EventHandlers;

/// <summary>#620: Notification's confirmation closes the round trip of an assignment email.</summary>
public class AssignmentEmailSentIntegrationEventHandlerTests
{
    private readonly IAssignmentEmailRepository _emails = Substitute.For<IAssignmentEmailRepository>();
    private readonly IAppraisalUnitOfWork _uow = Substitute.For<IAppraisalUnitOfWork>();

    private Task ConfirmAsync(AssignmentEmail email)
    {
        _emails.GetByIdAsync(email.Id, Arg.Any<CancellationToken>()).Returns(email);
        var ctx = Substitute.For<ConsumeContext<AssignmentEmailSentIntegrationEvent>>();
        ctx.Message.Returns(new AssignmentEmailSentIntegrationEvent { AppraisalId = email.AppraisalId, EmailId = email.Id });
        ctx.CancellationToken.Returns(CancellationToken.None);
        return new AssignmentEmailSentIntegrationEventHandler(_emails, _uow).Consume(ctx);
    }

    private static AssignmentEmail Chosen() =>
        AssignmentEmail.ToSend(Guid.NewGuid(), Guid.NewGuid(), [], [], "subject", "<p>body</p>", []);

    [Fact]
    public async Task A_queued_email_becomes_sent()
    {
        var email = Chosen();
        email.Queue("บริษัท แอคมี จำกัด", "สมชาย ใจดี");

        await ConfirmAsync(email);

        Assert.Equal(AssignmentEmailStatus.Sent, email.Status);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Only_a_handed_over_email_can_be_confirmed()
    {
        var email = Chosen(); // still Pending: never handed to Notification

        await ConfirmAsync(email);

        Assert.Equal(AssignmentEmailStatus.Pending, email.Status);
    }
}

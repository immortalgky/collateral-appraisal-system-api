using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Application.EventHandlers;
using Notification.Contracts.Email;
using Notification.Data;
using Notification.Infrastructure.Email.Attachments;
using Notification.Infrastructure.Email.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Notification.Tests.EventHandlers;

/// <summary>#620 Notification seam: the assignment email request comes in, the IEmailSender call goes out.</summary>
public class AssignmentEmailRequestedHandlerTests
{
    private static readonly Guid DocumentId = Guid.NewGuid();

    private readonly IEmailSender _sender = Substitute.For<IEmailSender>();
    private readonly IEmailTemplateRenderer _renderer = Substitute.For<IEmailTemplateRenderer>();
    private readonly IEmailAttachmentResolver _documents = Substitute.For<IEmailAttachmentResolver>();
    private readonly ConsumeContext<AssignmentEmailRequestedIntegrationEvent> _ctx =
        Substitute.For<ConsumeContext<AssignmentEmailRequestedIntegrationEvent>>();

    public AssignmentEmailRequestedHandlerTests()
    {
        _renderer.AssignmentEmail(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(c => $"<shell>{c.ArgAt<string?>(1)}</shell>");
        _documents.Type.Returns("document");
        _documents.ResolveAsync(DocumentId.ToString(), Arg.Any<CancellationToken>())
            .Returns([new EmailAttachment("deed.pdf", [1, 2, 3], "application/pdf")]);
        _ctx.MessageId.Returns((Guid?)null); // the real InboxGuard below never touches its provider-less context
        _ctx.CancellationToken.Returns(CancellationToken.None);
    }

    private static AssignmentEmailRequestedIntegrationEvent Request(IReadOnlyList<string>? to = null) => new()
    {
        AppraisalId = Guid.NewGuid(),
        EmailId = Guid.NewGuid(),
        To = to ?? ["jobs@acme.co.th", "extra@acme.co.th"],
        Cc = ["boss@bank.co.th"],
        Subject = "ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ 69042630",
        HtmlBody = "<p>เรียน บริษัท แอคมี จำกัด</p>",
        AttachmentRefs = [new EmailAttachmentRefData("document", DocumentId.ToString())],
        AssignedBy = "P5229"
    };

    private Task ConsumeAsync(AssignmentEmailRequestedIntegrationEvent request)
    {
        _ctx.Message.Returns(request);
        var inbox = new InboxGuard<NotificationDbContext>(
            new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().Options),
            NullLogger<InboxGuard<NotificationDbContext>>.Instance,
            Substitute.For<IDateTimeProvider>());
        return new AssignmentEmailRequestedHandler(
                _sender,
                _renderer,
                new EmailAttachmentAssembler([_documents], NullLogger<EmailAttachmentAssembler>.Instance),
                inbox,
                NullLogger<AssignmentEmailRequestedHandler>.Instance)
            .Consume(_ctx);
    }

    [Fact]
    public async Task Sends_the_email_logged_under_the_assignment_email_then_confirms_it()
    {
        var request = Request();

        await ConsumeAsync(request);

        await _sender.Received(1).SendAsync(Arg.Is<EmailMessage>(m =>
            m.Subject == request.Subject
            && m.HtmlBody == "<shell><p>เรียน บริษัท แอคมี จำกัด</p></shell>"
            && m.To!.SequenceEqual(new[] { "jobs@acme.co.th", "extra@acme.co.th" })
            && m.Cc!.SequenceEqual(new[] { "boss@bank.co.th" })
            && m.Attachments!.Single().FileName == "deed.pdf"
            && m.Source == "AssignmentEmail"
            && m.ReferenceId == request.EmailId.ToString()), Arg.Any<CancellationToken>());
        await _ctx.Received(1).Publish(
            Arg.Is<AssignmentEmailSentIntegrationEvent>(e => e.EmailId == request.EmailId && e.AppraisalId == request.AppraisalId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_send_is_not_confirmed_and_is_retried()
    {
        _sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP 451"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => ConsumeAsync(Request()));

        await _ctx.DidNotReceive().Publish(Arg.Any<AssignmentEmailSentIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_confirmation_does_not_send_the_company_a_second_email()
    {
        _ctx.Publish(Arg.Any<AssignmentEmailSentIntegrationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        await ConsumeAsync(Request()); // does not throw, so the bus does not retry the send

        await _sender.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_recipient_sends_nothing()
    {
        await ConsumeAsync(Request(to: []) with { Cc = [] });

        await _sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _ctx.DidNotReceive().Publish(Arg.Any<AssignmentEmailSentIntegrationEvent>(), Arg.Any<CancellationToken>());
    }
}

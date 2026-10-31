using Appraisal.Application.EventHandlers;
using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Auth.Contracts.Companies;
using Auth.Contracts.Users;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.EventHandlers;

/// <summary>
/// #620 dispatch seam: an EXTO case landing on the keyer (InternalAssigned, method Offline) goes in, the request for
/// the assignment email comes out of the outbox.
/// </summary>
public class ExtoAssignmentEmailDispatchTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();

    private readonly AppraisalAggregate _appraisal;
    private readonly AppraisalAssignment _assignment;
    private readonly IAppraisalUnitOfWork _uow = Substitute.For<IAppraisalUnitOfWork>();
    private readonly IAssignmentEmailRepository _emails = Substitute.For<IAssignmentEmailRepository>();
    private readonly IAssignmentEmailCaseReader _caseReader = Substitute.For<IAssignmentEmailCaseReader>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly IUserLookupService _users = Substitute.For<IUserLookupService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();

    public ExtoAssignmentEmailDispatchTests()
    {
        _appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        _appraisal.AssignAdmin();
        _assignment = _appraisal.Assignments.Single();
        // What AssignAppraisalCommandHandler recorded before the workflow landed on int-offline-book-keyin.
        _assignment.SetAssigner("P5229");
        _assignment.SetAssigneeCompany(CompanyId.ToString());

        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, "jobs@acme.co.th", "บริษัท แอคมี จำกัด"));
        _users.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, UserLookupDto> { ["K0001"] = new("K0001", "สมชาย", "ใจดี") });
    }

    private AssignmentEmail PendingRow() => AssignmentEmail.ToSend(
        _appraisal.Id,
        _assignment.Id,
        ["extra@acme.co.th"],
        [AssignmentEmailCcRoles.IntAdminAll, "boss@bank.co.th"],
        "ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ 69042630",
        "<p>เรียน {{company}}</p><p>ผู้รับผิดชอบ : {{responsible}}</p>",
        [new AssignmentEmailAttachment(DocumentId, AssignmentEmailAttachmentSources.Request)]);

    private Task LandOnKeyerAsync() => BuildHandler().Consume(Context(new InternalAssignedIntegrationEvent
    {
        AppraisalId = _appraisal.Id,
        AssigneeUserId = "K0001",
        AssignmentMethod = AppraisalAssignment.OfflineAssignmentMethod,
        AssignmentType = "External",
        CompletedBy = "P5229"
    }));

    private AssignmentEmailRequestedIntegrationEvent? Published()
    {
        var call = _outbox.ReceivedCalls()
            .SingleOrDefault(c => c.GetArguments()[0] is AssignmentEmailRequestedIntegrationEvent);
        return call?.GetArguments()[0] as AssignmentEmailRequestedIntegrationEvent;
    }

    [Fact]
    public async Task Pending_row_is_filled_and_requested_with_literal_recipients_and_its_documents()
    {
        var row = PendingRow();
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>()).Returns(row);

        await LandOnKeyerAsync();

        var sent = Published();
        Assert.NotNull(sent);
        Assert.Equal(_appraisal.Id, sent.AppraisalId);
        Assert.Equal(row.Id, sent.EmailId);
        Assert.Equal(["jobs@acme.co.th", "extra@acme.co.th"], sent.To);
        Assert.Equal(["boss@bank.co.th"], sent.Cc); // role tokens are expanded in a later slice (#621)
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ 69042630", sent.Subject);
        Assert.Equal("<p>เรียน บริษัท แอคมี จำกัด</p><p>ผู้รับผิดชอบ : สมชาย ใจดี</p>", sent.HtmlBody);
        Assert.Equal([new EmailAttachmentRefData("document", DocumentId.ToString())], sent.AttachmentRefs);
        Assert.Equal("P5229", sent.AssignedBy);
        // The row holds what was sent, Queued (never picked again) until Notification confirms.
        Assert.Equal(sent.HtmlBody, row.BodyHtml);
        Assert.Equal(AssignmentEmailStatus.Queued, row.Status);
        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>()); // the handover, then the email
    }

    [Fact]
    public async Task Assigned_without_sending_requests_no_email()
    {
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>())
            .Returns(AssignmentEmail.NotSent(_appraisal.Id, _assignment.Id));

        await LandOnKeyerAsync();

        Assert.Null(Published());
    }

    [Fact]
    public async Task No_choice_recorded_sends_the_bank_template_as_a_new_pending_row()
    {
        // Read before this save: the stored row has the company but not yet the keyer.
        _caseReader.GetCaseAsync(_appraisal.Id, Arg.Any<CancellationToken>()).Returns(new AssignmentEmailCase(
            AppraisalNumber: "69042630", CustomerName: "นายสมศักดิ์ มั่นคง", ContactName: "คุณวิไล",
            ContactPhone: "081-234-5678", FeePaymentText: null, AssignmentMethod: "Offline",
            AssigneeCompanyId: CompanyId.ToString(), KeyerName: null, FollowupMethod: null, FollowupName: null,
            Remark: null));
        _sender.Send(Arg.Any<GetFeePreviewQuery>(), Arg.Any<CancellationToken>())
            .Returns(new FeePreviewResult(null, null, new FeeBaseSourceDto(FeeBaseKinds.None, null, null, null, null, null, null),
                null, [], false, 0m, 7m, 0m, 0m));
        AssignmentEmail? added = null;
        await _emails.AddAsync(Arg.Do<AssignmentEmail>(e => added = e), Arg.Any<CancellationToken>());

        await LandOnKeyerAsync();

        // The template reads the case after the handover is saved (keyer, Offline method, materialised fee), and the
        // email goes out in a save of its own.
        Received.InOrder(() =>
        {
            _uow.SaveChangesAsync(Arg.Any<CancellationToken>());
            _caseReader.GetCaseAsync(_appraisal.Id, Arg.Any<CancellationToken>());
            _uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        Assert.NotNull(added);
        Assert.Equal(AssignmentEmailStatus.Queued, added.Status);
        Assert.Equal(_assignment.Id, added.AssignmentId);
        var sent = Published();
        Assert.NotNull(sent);
        Assert.Equal(added.Id, sent.EmailId);
        Assert.Equal(["jobs@acme.co.th"], sent.To);
        Assert.Empty(sent.Cc); // the template's Cc is role tokens only
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ มั่นคง 69042630", sent.Subject);
        Assert.Contains("<p>เรียน บริษัท แอคมี จำกัด</p>", sent.HtmlBody);
        Assert.Contains("ผู้รับผิดชอบ : สมชาย ใจดี<br>", sent.HtmlBody);
        Assert.Empty(sent.AttachmentRefs);
    }

    [Fact]
    public async Task A_redelivered_or_routed_back_landing_does_not_send_again()
    {
        // The first landing already moved the row past Pending (a crash before the inbox mark, or a route-back from
        // int-appraisal-check re-firing the event).
        _assignment.Assign("External", assigneeUserId: "K0001", assigneeCompanyId: CompanyId.ToString(),
            assignmentMethod: AppraisalAssignment.OfflineAssignmentMethod, assignedBy: "P5229");
        _assignment.StartWork();
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>()).Returns(PendingRow());

        await LandOnKeyerAsync();

        Assert.Null(Published());
    }

    [Fact]
    public async Task The_assignment_is_saved_even_when_the_email_cannot_be_prepared()
    {
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>()).Returns(PendingRow());
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns<CompanyLookupDto?>(_ => throw new TimeoutException("auth lookup timed out"));

        await LandOnKeyerAsync();

        Assert.Null(Published());
        Assert.Equal(AssignmentStatus.InProgress, _assignment.AssignmentStatus);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private InternalAssignedIntegrationEventHandler BuildHandler()
    {
        var repo = Substitute.For<IAppraisalRepository>();
        repo.GetByIdWithAllDataAsync(_appraisal.Id, Arg.Any<CancellationToken>()).Returns(_appraisal);

        var fees = Substitute.For<IAssignmentFeeService>();
        fees.ResolveSourceForAppraisalAsync(Arg.Any<AppraisalAggregate>(), Arg.Any<AssignmentFeeSource>(), Arg.Any<CancellationToken>())
            .Returns(new AssignmentFeeSource.TierBased());

        // null MessageId (Context below) → the guard never touches this provider-less context.
        var inboxGuard = new InboxGuard<AppraisalDbContext>(
            new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>().Options),
            NullLogger<InboxGuard<AppraisalDbContext>>.Instance,
            Substitute.For<IDateTimeProvider>());

        var composer = new AssignmentEmailComposer(_caseReader, _companies, _sender, new ConfigurationBuilder().Build());
        var dispatcher = new AssignmentEmailDispatcher(_emails, composer, _companies, _users, _outbox);

        return new InternalAssignedIntegrationEventHandler(
            repo, _uow, fees, NullLogger<InternalAssignedIntegrationEventHandler>.Instance, inboxGuard, dispatcher);
    }

    /// <summary>For handler tests not about the email: the assign recorded none.</summary>
    internal static AssignmentEmailDispatcher NoEmailDispatcher() => new(
        Substitute.For<IAssignmentEmailRepository>(),
        new AssignmentEmailComposer(Substitute.For<IAssignmentEmailCaseReader>(), Substitute.For<ICompanyLookupService>(),
            Substitute.For<ISender>(), new ConfigurationBuilder().Build()),
        Substitute.For<ICompanyLookupService>(),
        Substitute.For<IUserLookupService>(),
        Substitute.For<IIntegrationEventOutbox>());

    private static ConsumeContext<InternalAssignedIntegrationEvent> Context(InternalAssignedIntegrationEvent message)
    {
        var ctx = Substitute.For<ConsumeContext<InternalAssignedIntegrationEvent>>();
        ctx.Message.Returns(message);
        ctx.MessageId.Returns((Guid?)null);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }
}

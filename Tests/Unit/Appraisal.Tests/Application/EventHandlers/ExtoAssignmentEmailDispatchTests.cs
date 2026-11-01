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
        // What Auth answers for the Cc role chips (it already drops unusable users and blank emails).
        _users.GetEmailsInRoleAsync("IntAdmin", Arg.Any<CancellationToken>())
            .Returns(["admin1@bank.co.th", "Boss@bank.co.th"]);
        _users.GetEmailsAsync(Arg.Is<IEnumerable<string>>(u => u.SequenceEqual(new[] { "K0001" })), Arg.Any<CancellationToken>())
            .Returns(["keyer@bank.co.th"]);
        _users.GetSupervisorEmailsAsync("K0001", Arg.Any<CancellationToken>())
            .Returns(["lead@bank.co.th", "admin1@bank.co.th"]);
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
        // The IntAdmin chip expanded, the literal kept, an address both give listed once.
        Assert.Equal(["admin1@bank.co.th", "Boss@bank.co.th"], sent.Cc);
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ 69042630", sent.Subject);
        Assert.Equal("<p>เรียน บริษัท แอคมี จำกัด</p><p>ผู้รับผิดชอบ : สมชาย ใจดี</p>", sent.HtmlBody);
        Assert.Equal([new EmailAttachmentRefData("document", DocumentId.ToString())], sent.AttachmentRefs);
        Assert.Equal("P5229", sent.AssignedBy);
        // The row holds what was sent, Queued (never picked again) until Notification confirms.
        Assert.Equal(sent.HtmlBody, row.BodyHtml);
        Assert.Equal(AssignmentEmailStatus.Queued, row.Status);
        // Queued before the handover's one save, so the row and its outbox message commit with it.
        Received.InOrder(() =>
        {
            _outbox.Publish(Arg.Any<AssignmentEmailRequestedIntegrationEvent>(), Arg.Any<string?>(),
                Arg.Any<Dictionary<string, string>?>());
            _uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Removed_role_chips_are_not_expanded()
    {
        var row = AssignmentEmail.ToSend(_appraisal.Id, _assignment.Id, [], ["boss@bank.co.th"], "subject", "<p>body</p>", []);
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>()).Returns(row);

        await LandOnKeyerAsync();

        Assert.Equal(["boss@bank.co.th"], Published()!.Cc);
        await _users.DidNotReceive().GetEmailsInRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _users.DidNotReceive().GetEmailsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await _users.DidNotReceive().GetSupervisorEmailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("  ")]
    [InlineData(" ; , ")] // separators only: the Notification consumer would find no address in it
    public async Task A_company_without_an_email_fails_the_email_and_sends_nothing(string companyEmail)
    {
        var row = AssignmentEmail.ToSend(_appraisal.Id, _assignment.Id, [], [AssignmentEmailCcRoles.IntAdminAll],
            "subject", "<p>body</p>", []);
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>()).Returns(row);
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, companyEmail, "บริษัท แอคมี จำกัด"));

        await LandOnKeyerAsync();

        Assert.Null(Published());
        Assert.Equal(AssignmentEmailStatus.Failed, row.Status);
        Assert.Contains("no email address", row.FailureReason);
        // The handover itself still saves, with the Failed row.
        Assert.Equal(AssignmentStatus.InProgress, _assignment.AssignmentStatus);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_company_fails_the_email_with_its_own_reason()
    {
        var row = AssignmentEmail.ToSend(_appraisal.Id, _assignment.Id, [], [], "subject", "<p>body</p>", []);
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>()).Returns(row);
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>()).Returns((CompanyLookupDto?)null);

        await LandOnKeyerAsync();

        Assert.Null(Published());
        Assert.Equal(AssignmentEmailStatus.Failed, row.Status);
        Assert.Contains("No company is assigned", row.FailureReason);
    }

    [Fact]
    public async Task A_master_data_address_list_is_sent_one_address_each()
    {
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>()).Returns(PendingRow());
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, "jobs@acme.co.th; ops@acme.co.th", null));

        await LandOnKeyerAsync();

        Assert.Equal(["jobs@acme.co.th", "ops@acme.co.th", "extra@acme.co.th"], Published()!.To);
    }

    [Fact]
    public async Task Assigned_without_sending_requests_no_email()
    {
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>())
            .Returns(AssignmentEmail.NotSent(_appraisal.Id, _assignment.Id));

        await LandOnKeyerAsync();

        Assert.Null(Published());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_choice_recorded_sends_the_bank_template_as_a_new_pending_row()
    {
        // Read before this handler's save: the stored row is the one routed back, still naming the previous keyer and
        // the previous decision's method. The handover being saved is what the email must describe.
        _caseReader.GetCaseAsync(_appraisal.Id, Arg.Any<CancellationToken>()).Returns(new AssignmentEmailCase(
            AppraisalNumber: "69042630", CustomerName: "นายสมศักดิ์ มั่นคง", ContactName: "คุณวิไล",
            ContactPhone: "081-234-5678", FeePaymentText: null, AssignmentMethod: "Manual",
            AssigneeCompanyId: CompanyId.ToString(), KeyerName: "คนคีย์ เดิม", FollowupMethod: "Manual",
            FollowupName: "เจ้าหน้าที่ ติดตามเดิม", Remark: null));
        _sender.Send(Arg.Any<GetFeePreviewQuery>(), Arg.Any<CancellationToken>())
            .Returns(new FeePreviewResult(null, null, new FeeBaseSourceDto(FeeBaseKinds.None, null, null, null, null, null, null),
                null, [], false, 0m, 7m, 0m, 0m));
        AssignmentEmail? added = null;
        await _emails.AddAsync(Arg.Do<AssignmentEmail>(e => added = e), Arg.Any<CancellationToken>());

        await LandOnKeyerAsync();

        Assert.NotNull(added);
        Assert.Equal(AssignmentEmailStatus.Queued, added.Status);
        Assert.Equal(_assignment.Id, added.AssignmentId);
        var sent = Published();
        Assert.NotNull(sent);
        Assert.Equal(added.Id, sent.EmailId);
        Assert.Equal(["jobs@acme.co.th"], sent.To);
        // The template's EXTO chips: every IntAdmin, the keyer, the keyer's supervisors.
        Assert.Equal(["admin1@bank.co.th", "Boss@bank.co.th", "keyer@bank.co.th", "lead@bank.co.th"], sent.Cc);
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ มั่นคง 69042630", sent.Subject);
        Assert.Contains("<p>เรียน บริษัท แอคมี จำกัด</p>", sent.HtmlBody);
        Assert.Contains("ผู้รับผิดชอบ : สมชาย ใจดี<br>", sent.HtmlBody);
        Assert.Equal(
            [AssignmentEmailCcRoles.IntAdminAll, AssignmentEmailCcRoles.Keyer, AssignmentEmailCcRoles.KeyerSupervisor],
            added.Cc); // EXTO's Cc roles (expanded by #621), not the follow-up ones
        Assert.Empty(sent.AttachmentRefs);
        // Priced as this handover bills it, the same question AssignAppraisal asked.
        await _sender.Received().Send(
            Arg.Is<GetFeePreviewQuery>(q => q.AppraisalId == _appraisal.Id && q.AssignmentType == "external-offline"
                                            && q.AssignmentMethod == "manual" && q.CompanyId == CompanyId),
            Arg.Any<CancellationToken>());
        // One save: the handover, the email row and its outbox message commit together.
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        var dispatcher = new AssignmentEmailDispatcher(_emails, composer, _companies, _users, _outbox,
            NullLogger<AssignmentEmailDispatcher>.Instance);

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
        Substitute.For<IIntegrationEventOutbox>(),
        NullLogger<AssignmentEmailDispatcher>.Instance);

    private static ConsumeContext<InternalAssignedIntegrationEvent> Context(InternalAssignedIntegrationEvent message)
    {
        var ctx = Substitute.For<ConsumeContext<InternalAssignedIntegrationEvent>>();
        ctx.Message.Returns(message);
        ctx.MessageId.Returns((Guid?)null);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }
}

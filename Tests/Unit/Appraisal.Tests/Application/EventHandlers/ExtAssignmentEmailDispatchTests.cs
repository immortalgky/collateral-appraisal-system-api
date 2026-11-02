using Appraisal.Application.EventHandlers;
using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
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
/// #622 dispatch seam: an in-system external (EXT) case gets its company (CompanyAssigned), then its follow-up officer
/// (InternalFollowupAssigned), in that order on the partitioned appraisal-sync endpoint; the request for the
/// assignment email comes out of the outbox after the second.
/// </summary>
public class ExtAssignmentEmailDispatchTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private readonly AppraisalAggregate _appraisal;
    private readonly AppraisalAssignment _assignment;
    private readonly List<AssignmentEmail> _rows = [];
    private readonly IAssignmentEmailRepository _emails = Substitute.For<IAssignmentEmailRepository>();
    private readonly IAssignmentEmailCaseReader _caseReader = Substitute.For<IAssignmentEmailCaseReader>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly IUserLookupService _users = Substitute.For<IUserLookupService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IQuotationRepository _quotations = Substitute.For<IQuotationRepository>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();

    public ExtAssignmentEmailDispatchTests()
    {
        _appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        _appraisal.SetAppraisalNumber("69042630");
        _appraisal.AssignAdmin();
        _assignment = _appraisal.Assignments.Single();

        // The email rows of this case, as the repository would hold them across the two consumers.
        _emails.AddAsync(Arg.Do<AssignmentEmail>(_rows.Add), Arg.Any<CancellationToken>());
        _emails.GetLatestUnsentAsync(_assignment.Id, Arg.Any<CancellationToken>()).Returns(_ =>
            _rows.LastOrDefault(r => r.Status is AssignmentEmailStatus.Pending or AssignmentEmailStatus.NotSent));

        _emails.GetHandedOverRecipientsAsync(_assignment.Id, Arg.Any<CancellationToken>()).Returns(_ =>
            _rows.Where(r => r.Status is AssignmentEmailStatus.Queued or AssignmentEmailStatus.Sent).Select(r => r.SentTo).ToList());

        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, "jobs@acme.co.th", "บริษัท แอคมี จำกัด"));
        _users.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, UserLookupDto> { ["F0001"] = new("F0001", "สมหญิง", "ตามงาน") });
        _users.GetEmailsInGroupAsync("IntAdmin", Arg.Any<CancellationToken>()).Returns(["admin1@bank.co.th"]);
        _users.GetEmailsAsync(Arg.Is<IEnumerable<string>>(u => u.SequenceEqual(new[] { "F0001" })), Arg.Any<CancellationToken>())
            .Returns(["followup@bank.co.th"]);
        _users.GetSupervisorEmailsAsync("F0001", Arg.Any<CancellationToken>()).Returns(["lead@bank.co.th"]);
    }

    /// <summary>What AssignAppraisalCommandHandler wrote when the admin pressed มอบหมายและส่ง.</summary>
    private AssignmentEmail AdminRow()
    {
        _assignment.SetAssigner("P5229");
        var row = AssignmentEmail.ToSend(
            _appraisal.Id, _assignment.Id, [],
            [AssignmentEmailCcRoles.IntAdminAll, AssignmentEmailCcRoles.FollowupStaff, AssignmentEmailCcRoles.FollowupSupervisor],
            "ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ 69042630",
            "<p>เรียน {{company}}</p><p>ผู้รับผิดชอบ : {{internal_staff}}</p>", []);
        _rows.Add(row);
        return row;
    }

    private async Task AssignCompanyAndFollowupAsync(string method, string? officer = "F0001")
    {
        await BuildCompanyHandler().Consume(Context(new CompanyAssignedIntegrationEvent
        {
            AppraisalId = _appraisal.Id,
            CompanyId = CompanyId,
            CompanyName = "Acme Appraisal",
            AssignmentMethod = method
        }));
        await BuildFollowupHandler().Consume(Context(new InternalFollowupAssignedIntegrationEvent
        {
            AppraisalId = _appraisal.Id,
            InternalAppraiserId = officer,
            InternalFollowupAssignmentMethod = officer is null ? null : "RoundRobin"
        }));
    }

    private List<AssignmentEmailRequestedIntegrationEvent> Published() => _outbox.ReceivedCalls()
        .Select(c => c.GetArguments()[0]).OfType<AssignmentEmailRequestedIntegrationEvent>().ToList();

    [Fact]
    public async Task Admin_assigned_email_is_sent_once_the_followup_is_known_with_officer_and_supervisor_in_Cc()
    {
        var row = AdminRow();

        await AssignCompanyAndFollowupAsync("Manual");

        var sent = Assert.Single(Published());
        Assert.Equal(row.Id, sent.EmailId);
        Assert.Equal(["jobs@acme.co.th"], sent.To);
        Assert.Equal(["admin1@bank.co.th", "followup@bank.co.th", "lead@bank.co.th"], sent.Cc);
        Assert.Equal("<p>เรียน บริษัท แอคมี จำกัด</p><p>ผู้รับผิดชอบ : สมหญิง ตามงาน</p>", sent.HtmlBody);
        Assert.Equal("P5229", sent.AssignedBy);
        Assert.Equal("69042630", sent.AppraisalNumber);
        Assert.Equal(AssignmentEmailStatus.Queued, row.Status);
        Assert.Single(_rows); // the admin's row, no template row next to it
    }

    [Theory]
    [InlineData("RoundRobin", "manual")]
    [InlineData("Quotation", "quotation")]
    [InlineData("Forced", "manual")] // Construction-Inspection carry-over
    public async Task A_system_assignment_sends_the_template_to_the_workflow_chosen_company(string method, string feeMethod)
    {
        StubTemplateReads();
        _quotations.GetFinalizedByAppraisalIdAsync(_appraisal.Id, CompanyId, Arg.Any<CancellationToken>())
            .Returns(WonQuotation());

        await AssignCompanyAndFollowupAsync(method);

        var row = Assert.Single(_rows);
        var sent = Assert.Single(Published());
        Assert.Equal(row.Id, sent.EmailId);
        Assert.Equal(AssignmentEmailStatus.Queued, row.Status);
        Assert.Equal(["jobs@acme.co.th"], sent.To);
        Assert.Equal(["admin1@bank.co.th", "followup@bank.co.th", "lead@bank.co.th"], sent.Cc);
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ มั่นคง 69042630", sent.Subject);
        Assert.Contains("<p>เรียน บริษัท แอคมี จำกัด<br><br>", sent.HtmlBody);
        Assert.Contains("ผู้รับผิดชอบ: สมหญิง ตามงาน<br>", sent.HtmlBody);
        Assert.Equal("บริษัท แอคมี จำกัด", sent.CompanyName);
        // Priced for the company this assignment bills, not the stored row's.
        await _sender.Received().Send(
            Arg.Is<GetFeePreviewQuery>(q => q.AppraisalId == _appraisal.Id && q.AssignmentType == "external"
                                            && q.AssignmentMethod == feeMethod && q.CompanyId == CompanyId),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Manual")] // the admin's manual pick re-publishes CompanyAssigned on the way back
    [InlineData("RoundRobin")]
    public async Task A_route_back_replay_of_the_company_the_case_holds_sends_nothing(string method)
    {
        // The first engagement's email went out; the route-back left the row Assigned, holding the company.
        var first = AdminRow();
        _assignment.Assign("External", assigneeCompanyId: CompanyId.ToString(), assignmentMethod: method,
            internalAppraiserId: "F0001", internalFollowupMethod: "RoundRobin", assignedBy: "P5229");
        first.Queue("บริษัท แอคมี จำกัด", "สมหญิง ตามงาน", ["jobs@acme.co.th"], []);
        first.MarkSent(DateTime.Now);

        await AssignCompanyAndFollowupAsync(method);

        Assert.Empty(Published());
        Assert.Single(_rows);
    }

    [Fact]
    public async Task A_pending_relanding_on_the_same_company_with_no_choice_does_not_send_the_template_again()
    {
        // The summary page's proceed after a route-back: the row is Pending again, holding the company that already got its email.
        var first = AdminRow();
        first.Queue("บริษัท แอคมี จำกัด", "สมหญิง ตามงาน", ["jobs@acme.co.th"], []);
        first.MarkSent(DateTime.Now);
        _assignment.SetAssigneeCompany(CompanyId.ToString());
        StubTemplateReads();

        await AssignCompanyAndFollowupAsync("RoundRobin");

        Assert.Empty(Published());
        Assert.Single(_rows);
    }

    [Fact]
    public async Task A_pending_relanding_after_an_email_that_failed_sends_the_template()
    {
        var first = AdminRow();
        first.FailToPrepare("ระบบเตรียมอีเมลไม่สำเร็จ");
        _assignment.SetAssigneeCompany(CompanyId.ToString());
        StubTemplateReads();

        await AssignCompanyAndFollowupAsync("RoundRobin");

        Assert.Single(Published());
        Assert.Equal(2, _rows.Count);
    }

    [Fact]
    public async Task A_route_back_that_lands_on_a_different_company_sends_it_the_template()
    {
        // The automatic route-back (initiation-check → initial-routing) leaves the row Assigned to the old company;
        // round-robin then picked another one (the banking segment changed).
        var first = AdminRow();
        _assignment.Assign("External", assigneeCompanyId: Guid.NewGuid().ToString(), assignmentMethod: "RoundRobin",
            internalAppraiserId: "F0001", internalFollowupMethod: "RoundRobin", assignedBy: "P5229");
        first.Queue("บริษัท เดิม จำกัด", "สมหญิง ตามงาน", ["old@company.co.th"], []);
        first.MarkSent(DateTime.Now);
        StubTemplateReads();

        await AssignCompanyAndFollowupAsync("RoundRobin");

        var sent = Assert.Single(Published());
        Assert.Equal(["jobs@acme.co.th"], sent.To);
        Assert.Contains("<p>เรียน บริษัท แอคมี จำกัด<br><br>", sent.HtmlBody);
        Assert.Equal(2, _rows.Count);
    }

    [Fact]
    public async Task No_followup_found_still_sends_without_the_followup_Cc_and_a_dash_as_responsible()
    {
        var row = AdminRow();

        await AssignCompanyAndFollowupAsync("Manual", officer: null);

        var sent = Assert.Single(Published());
        Assert.Equal(["jobs@acme.co.th"], sent.To);
        Assert.Equal(["admin1@bank.co.th"], sent.Cc);
        Assert.Equal("<p>เรียน บริษัท แอคมี จำกัด</p><p>ผู้รับผิดชอบ : -</p>", sent.HtmlBody);
        Assert.Null(_assignment.InternalAppraiserId);
        Assert.Equal(AssignmentEmailStatus.Queued, row.Status);
    }

    [Fact]
    public async Task An_email_that_cannot_be_prepared_is_marked_Failed_and_the_followup_still_saves()
    {
        var row = AdminRow();
        _users.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<string, UserLookupDto>>(_ => throw new TimeoutException("auth lookup timed out"));

        await AssignCompanyAndFollowupAsync("Manual");

        Assert.Empty(Published());
        // Never left Pending: the slip shows it failed and the admin can send it again (#623).
        Assert.Equal(AssignmentEmailStatus.Failed, row.Status);
        Assert.Equal("ระบบเตรียมอีเมลไม่สำเร็จ", row.FailureReason);
        Assert.Equal("F0001", _assignment.InternalAppraiserId);
    }

    [Fact]
    public async Task A_template_that_cannot_be_built_leaves_an_empty_Failed_row_and_the_assignment_saves()
    {
        StubTemplateReads();
        _sender.Send(Arg.Any<GetFeePreviewQuery>(), Arg.Any<CancellationToken>())
            .Returns<FeePreviewResult>(_ => throw new TimeoutException("fee preview timed out"));

        await AssignCompanyAndFollowupAsync("RoundRobin");

        Assert.Empty(Published());
        // Shown on the slip as failed, so the admin sends it by hand (content from the preview).
        var row = Assert.Single(_rows);
        Assert.Equal(AssignmentEmailStatus.Failed, row.Status);
        Assert.Equal("ระบบเตรียมอีเมลไม่สำเร็จ", row.FailureReason);
        Assert.Equal("", row.Subject);
        Assert.Equal(CompanyId.ToString(), _assignment.AssigneeCompanyId);
    }

    [Fact]
    public async Task Assigned_without_sending_requests_no_email()
    {
        _rows.Add(AssignmentEmail.NotSent(_appraisal.Id, _assignment.Id));

        await AssignCompanyAndFollowupAsync("Manual");

        Assert.Empty(Published());
        Assert.Single(_rows);
    }

    private void StubTemplateReads()
    {
        // Read while CompanyAssigned runs: the stored row still names an earlier decision's company and officer.
        _caseReader.GetCaseAsync(_appraisal.Id, Arg.Any<CancellationToken>()).Returns(new AssignmentEmailCase(
            AppraisalNumber: "69042630", CustomerName: "นายสมศักดิ์ มั่นคง", ContactName: "คุณวิไล",
            ContactPhone: "081-234-5678", FeePaymentText: null, AssignmentMethod: "Manual",
            AssigneeCompanyId: Guid.NewGuid().ToString(), KeyerName: null, FollowupMethod: "Manual",
            FollowupName: "เจ้าหน้าที่ ติดตามเดิม", Remark: null));
        _sender.Send(Arg.Any<GetFeePreviewQuery>(), Arg.Any<CancellationToken>())
            .Returns(new FeePreviewResult(null, null, new FeeBaseSourceDto(FeeBaseKinds.None, null, null, null, null, null, null),
                null, [], false, 0m, 7m, 0m, 0m));
    }

    private QuotationRequest WonQuotation()
    {
        var rfq = QuotationRequest.Create(DateTime.Now.AddDays(1), "admin", DateTime.Now);
        var winner = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), CompanyId, "QT-001", DateTime.Now);
        winner.AddItem(Guid.NewGuid(), _appraisal.Id, 1, 50_000m, 3);
        winner.MarkAsWinner();
        rfq.AddQuotation(winner);
        return rfq;
    }

    private CompanyAssignedIntegrationEventHandler BuildCompanyHandler()
    {
        var fees = Substitute.For<IAssignmentFeeService>();
        fees.ResolveSourceForAppraisalAsync(Arg.Any<AppraisalAggregate>(), Arg.Any<AssignmentFeeSource>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<AssignmentFeeSource>(1));
        return new CompanyAssignedIntegrationEventHandler(
            Repository(), _quotations, Substitute.For<IAppraisalUnitOfWork>(), fees,
            NullLogger<CompanyAssignedIntegrationEventHandler>.Instance, InboxGuard(), Dispatcher());
    }

    private InternalFollowupAssignedIntegrationEventHandler BuildFollowupHandler() => new(
        Repository(), Substitute.For<IAppraisalUnitOfWork>(),
        NullLogger<InternalFollowupAssignedIntegrationEventHandler>.Instance, InboxGuard(), Dispatcher());

    private IAppraisalRepository Repository()
    {
        var repo = Substitute.For<IAppraisalRepository>();
        repo.GetByIdWithAllDataAsync(_appraisal.Id, Arg.Any<CancellationToken>()).Returns(_appraisal);
        return repo;
    }

    private AssignmentEmailDispatcher Dispatcher() => new(
        _emails, new AssignmentEmailComposer(_caseReader, _companies, _users, _sender, new ConfigurationBuilder().Build()),
        _companies, _users, _outbox, NullLogger<AssignmentEmailDispatcher>.Instance);

    // null MessageId (Context below) → the guard never touches this provider-less context.
    private static InboxGuard<AppraisalDbContext> InboxGuard() => new(
        new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>().Options),
        NullLogger<InboxGuard<AppraisalDbContext>>.Instance,
        Substitute.For<IDateTimeProvider>());

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.MessageId.Returns((Guid?)null);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }
}

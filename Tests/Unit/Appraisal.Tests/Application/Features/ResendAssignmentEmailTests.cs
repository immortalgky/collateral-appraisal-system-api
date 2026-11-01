using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Application.Features.Assignments.ResendAssignmentEmail;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Auth.Contracts.Companies;
using Auth.Contracts.Users;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Tests.Application.Features;

/// <summary>#623: "ส่งอีกครั้ง" / "ส่งอีเมล" from the read-only slip goes in, a new email row and its send request come out.</summary>
public class ResendAssignmentEmailTests
{
    private static readonly Guid AppraisalId = Guid.NewGuid();
    private static readonly Guid AssignmentId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();

    private readonly IAssignmentEmailCaseReader _caseReader = Substitute.For<IAssignmentEmailCaseReader>();
    private readonly IAssignmentEmailRepository _emails = Substitute.For<IAssignmentEmailRepository>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly IUserLookupService _users = Substitute.For<IUserLookupService>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IAppraisalUnitOfWork _uow = Substitute.For<IAppraisalUnitOfWork>();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

    public ResendAssignmentEmailTests()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(true);
        _user.UserCode.Returns("P5229");
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, "jobs@acme.co.th", "บริษัท แอคมี จำกัด"));
        _caseReader.GetDocumentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new AssignmentEmailDocument(DocumentId, "deed.pdf", 1024)]);
        _users.GetEmailsInRoleAsync("IntAdmin", Arg.Any<CancellationToken>()).Returns(["admin@bank.co.th"]);
        Case();
    }

    private void Case(
        string appraisalStatus = "InProgress",
        string assignmentType = "External",
        string assignmentStatus = "Assigned",
        string assignmentMethod = "Offline",
        string? keyerName = "สมชาย ใจดี") =>
        _caseReader.GetCaseAsync(AppraisalId, Arg.Any<CancellationToken>()).Returns(new AssignmentEmailCase(
            AppraisalNumber: "69042630", CustomerName: "นายสมศักดิ์ มั่นคง", ContactName: "คุณวิไล",
            ContactPhone: "081-234-5678", FeePaymentText: null, AssignmentMethod: assignmentMethod,
            AssigneeCompanyId: CompanyId.ToString(), KeyerName: keyerName, FollowupMethod: null,
            FollowupName: null, Remark: null, AppraisalStatus: appraisalStatus, AssignmentId: AssignmentId,
            AssignmentType: assignmentType, AssignmentStatus: assignmentStatus));

    private void LatestUnsent(AssignmentEmail? email) =>
        _emails.GetLatestUnsentAsync(AssignmentId, Arg.Any<CancellationToken>()).Returns(email);

    private static AssignmentEmailInput Edited() => new(
        ["extra@acme.co.th"],
        [AssignmentEmailCcRoles.IntAdminAll, "boss@bank.co.th"],
        "ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ 69042630 (ส่งซ้ำ)",
        "<p>เรียน {{company}}</p><p>ผู้รับผิดชอบ : {{responsible}}</p>",
        [new AssignmentEmailAttachmentInput(DocumentId, AssignmentEmailAttachmentSources.Request)]);

    private ResendAssignmentEmailCommandHandler Handler()
    {
        var composer = new AssignmentEmailComposer(_caseReader, _companies, Substitute.For<ISender>(), _configuration);
        var dispatcher = new AssignmentEmailDispatcher(
            _emails, composer, _companies, _users, _outbox, NullLogger<AssignmentEmailDispatcher>.Instance);
        return new ResendAssignmentEmailCommandHandler(_user, _caseReader, _emails, dispatcher, _configuration, _uow);
    }

    private Task<ResendAssignmentEmailResult> ResendAsync() =>
        Handler().Handle(new ResendAssignmentEmailCommand(AppraisalId, Edited()), CancellationToken.None);

    private AssignmentEmailRequestedIntegrationEvent? Published() =>
        _outbox.ReceivedCalls()
            .Select(c => c.GetArguments()[0])
            .OfType<AssignmentEmailRequestedIntegrationEvent>()
            .SingleOrDefault();

    private async Task AssertNothingSentAsync()
    {
        Assert.Null(Published());
        await _emails.DidNotReceive().AddAsync(Arg.Any<AssignmentEmail>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resend_after_a_sent_email_adds_a_queued_resend_row_and_requests_it()
    {
        AssignmentEmail? added = null;
        await _emails.AddAsync(Arg.Do<AssignmentEmail>(e => added = e), Arg.Any<CancellationToken>());

        var result = await ResendAsync();

        Assert.NotNull(added);
        Assert.Equal(added.Id, result.EmailId);
        Assert.True(added.IsResend);
        Assert.Equal(AssignmentEmailStatus.Queued, added.Status);
        Assert.Equal(AssignmentId, added.AssignmentId);
        Assert.Equal("<p>เรียน บริษัท แอคมี จำกัด</p><p>ผู้รับผิดชอบ : สมชาย ใจดี</p>", added.BodyHtml);

        var sent = Published();
        Assert.NotNull(sent);
        Assert.Equal(added.Id, sent.EmailId);
        Assert.Equal(["jobs@acme.co.th", "extra@acme.co.th"], sent.To);
        // The Cc chips expand to addresses at the resend too (#621).
        Assert.Equal(["admin@bank.co.th", "boss@bank.co.th"], sent.Cc);
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ 69042630 (ส่งซ้ำ)", sent.Subject);
        Assert.Equal([new EmailAttachmentRefData("document", DocumentId.ToString())], sent.AttachmentRefs);
        // A failure notice (#624) goes to the admin who pressed ส่งอีกครั้ง.
        Assert.Equal("P5229", sent.AssignedBy);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_case_assigned_without_sending_can_send_later()
    {
        LatestUnsent(AssignmentEmail.NotSent(AppraisalId, AssignmentId));

        await ResendAsync();

        Assert.NotNull(Published());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_email_still_waiting_for_its_handover_is_not_sent_twice()
    {
        LatestUnsent(AssignmentEmail.ToSend(AppraisalId, AssignmentId, [], [], "subject", "<p>body</p>", []));

        await Assert.ThrowsAsync<BadRequestException>(ResendAsync);

        await AssertNothingSentAsync();
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task A_closed_case_is_refused(string appraisalStatus)
    {
        Case(appraisalStatus: appraisalStatus);

        await Assert.ThrowsAsync<BadRequestException>(ResendAsync);

        await AssertNothingSentAsync();
    }

    [Theory]
    [InlineData("Internal", "Assigned")] // INT: no company to email
    [InlineData("External", "Pending")] // the admin has not pressed มอบหมาย yet
    public async Task A_case_not_handed_to_a_company_is_refused(string assignmentType, string assignmentStatus)
    {
        Case(assignmentType: assignmentType, assignmentStatus: assignmentStatus);

        await Assert.ThrowsAsync<BadRequestException>(ResendAsync);

        await AssertNothingSentAsync();
    }

    [Fact]
    public async Task A_resend_that_would_reach_nobody_is_refused()
    {
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, null, "บริษัท แอคมี จำกัด"));
        var handler = Handler();

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new ResendAssignmentEmailCommand(AppraisalId, Edited() with { ExtraTo = [] }), CancellationToken.None));

        // Refused before the outbox: a later save in the same scope must have nothing to commit.
        await AssertNothingSentAsync();
    }

    [Fact]
    public async Task A_placeholder_still_unknown_at_send_is_refused()
    {
        Case(keyerName: null); // nobody to name as ผู้รับผิดชอบ

        var error = await Assert.ThrowsAsync<BadRequestException>(ResendAsync);

        Assert.Contains("{{responsible}}", error.Message);
        await AssertNothingSentAsync();
    }

    [Fact]
    public async Task An_unknown_value_is_fine_once_the_admin_removed_its_placeholder()
    {
        Case(keyerName: null);

        await Handler().Handle(new ResendAssignmentEmailCommand(AppraisalId,
            Edited() with { BodyHtml = "<p>เรียน {{company}}</p><p>ผู้รับผิดชอบ : คุณวิภา</p>" }), CancellationToken.None);

        Assert.NotNull(Published());
    }

    [Fact]
    public async Task A_company_that_is_gone_cannot_be_named_in_the_body()
    {
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>()).Returns((CompanyLookupDto?)null);

        // The extra To would still reach someone, but "เรียน {{company}}" would go out as is.
        var error = await Assert.ThrowsAsync<BadRequestException>(ResendAsync);

        Assert.Contains("{{company}}", error.Message);

        await AssertNothingSentAsync();
    }

    [Fact]
    public async Task A_non_admin_is_refused()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(false);

        await Assert.ThrowsAsync<ForbiddenException>(ResendAsync);

        await AssertNothingSentAsync();
    }

    [Fact]
    public async Task An_attachment_that_is_no_longer_a_document_is_refused()
    {
        _caseReader.GetDocumentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await Assert.ThrowsAsync<BadRequestException>(ResendAsync);

        await AssertNothingSentAsync();
    }
}

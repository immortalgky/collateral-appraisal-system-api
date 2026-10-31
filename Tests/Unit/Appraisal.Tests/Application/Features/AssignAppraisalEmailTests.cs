using Appraisal.Application.Features.Assignments.AssignAppraisal;
using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Features.Quotations.AutoAssign;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Auth.Contracts.Companies;
using MediatR;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
using Workflow;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// EXT / EXTO assign carries the email the admin edited in the dialog: sent (a Pending row), not sent (a NotSent
/// row) or, with no choice made (system paths), no row at all.
/// </summary>
public class AssignAppraisalEmailTests
{
    private static readonly Guid DocA = Guid.NewGuid();
    private static readonly Guid DocB = Guid.NewGuid();

    private static AssignmentEmailInput Email(
        List<AssignmentEmailAttachmentInput>? attachments = null,
        List<string>? extraTo = null,
        List<string>? cc = null,
        string subject = "ขอนำส่งงานประเมิน") =>
        new(extraTo ?? ["second@acme.co.th"],
            cc ?? ["IntAdminAll", "Keyer", "boss@lhbank.co.th"],
            subject,
            "<p>เรียน <b>Acme</b></p>",
            attachments ?? [new(DocA, "request"), new(DocB, "upload")]);

    private static AssignAppraisalCommand Command(
        string decision = "EXT", AssignmentEmailInput? email = null, bool? sendEmail = true) =>
        new(Guid.NewGuid(), Guid.NewGuid(), AssigneeCompanyId: Guid.NewGuid().ToString(), DecisionTaken: decision,
            Email: email, SendEmail: sendEmail);

    // ── validation ────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> Errors(AssignAppraisalCommand command) =>
        new AssignAppraisalCommandValidator().Validate(command).Errors.Select(e => e.ErrorMessage).ToList();

    private static IReadOnlyList<string> Errors(AssignmentEmailInput email) => Errors(Command(email: email));

    [Theory]
    [InlineData("EXT")]
    [InlineData("EXTO")]
    [InlineData("exto")]
    public void A_well_formed_email_to_send_passes_validation(string decision) =>
        Assert.Empty(Errors(Command(decision, Email())));

    [Fact]
    public void Sending_without_an_email_is_rejected() =>
        Assert.NotEmpty(Errors(Command(email: null, sendEmail: true)));

    [Fact]
    public void More_than_ten_attachments_are_rejected() =>
        Assert.Contains("At most 10 attachments can be sent.", Errors(Email(attachments: Enumerable.Range(0, 11)
            .Select(_ => new AssignmentEmailAttachmentInput(Guid.NewGuid(), "upload")).ToList())));

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Acme <jobs@acme.co.th>")]
    [InlineData("a@b.co;c@d.co")]
    public void A_malformed_extra_to_address_is_rejected(string address) =>
        Assert.NotEmpty(Errors(Email(extraTo: [address])));

    [Fact]
    public void An_address_longer_than_254_characters_is_rejected() =>
        Assert.NotEmpty(Errors(Email(cc: [new string('a', 250) + "@x.co"])));

    [Fact]
    public void Cc_takes_role_tokens_and_addresses_but_not_unknown_tokens() =>
        Assert.NotEmpty(Errors(Email(cc: ["IntAdminAll", "Everyone"])));

    [Fact]
    public void An_unknown_attachment_source_is_rejected() =>
        Assert.NotEmpty(Errors(Email(attachments: [new(DocA, "elsewhere")])));

    [Fact]
    public void A_null_attachment_entry_is_a_validation_error_not_a_crash() =>
        Assert.NotEmpty(Errors(Email(attachments: [null!])));

    [Fact]
    public void Ten_documents_named_more_than_once_are_still_ten_attachments() =>
        Assert.Empty(Errors(Email(attachments: Enumerable.Range(0, 10)
            .Select(_ => new AssignmentEmailAttachmentInput(Guid.NewGuid(), "upload"))
            .SelectMany(a => new[] { a, a }).ToList())));

    [Fact]
    public void Missing_lists_are_read_as_empty() =>
        Assert.Empty(Errors(new AssignmentEmailInput(null, null, "ขอนำส่งงานประเมิน", "<p>x</p>", null)));

    [Theory]
    [InlineData("ขอนำส่ง\nBcc: x@evil.co")]
    [InlineData("ขอนำส่ง\r")]
    [InlineData("ขอนำส่ง\u2028Bcc: x@evil.co")]
    [InlineData("ขอนำส่ง\u000b")]
    public void A_subject_with_a_line_break_is_rejected(string subject) =>
        Assert.NotEmpty(Errors(Email(subject: subject)));

    [Fact]
    public void A_blank_subject_is_rejected() =>
        Assert.NotEmpty(Errors(Email(subject: " ")));

    [Fact]
    public void The_template_subject_always_passes()
    {
        var subject = AssignmentEmailTemplate.Subject(new AssignmentEmailTemplate.Values(
            null, "นายสมศักดิ์\u0000\u001b\r\n มั่นคง", "69042630", null, null, null, null, null, null));

        Assert.Empty(Errors(Email(subject: subject)));
    }

    [Fact]
    public void An_email_assigned_without_sending_is_not_checked() =>
        Assert.Empty(Errors(Command(email: Email(extraTo: ["not-an-email"], subject: "a\nb"), sendEmail: false)));

    [Theory]
    [InlineData("INT")]
    [InlineData("C")]
    [InlineData("R")]
    [InlineData("P")]
    public void Other_decisions_ignore_the_email(string decision) =>
        Assert.Empty(Errors(Command(decision, Email(extraTo: ["not-an-email"]), sendEmail: true)));

    // ── assign ────────────────────────────────────────────────────────────────

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IAppraisalRepository _appraisals = Substitute.For<IAppraisalRepository>();
    private readonly IAppraisalUnitOfWork _uow = Substitute.For<IAppraisalUnitOfWork>();
    private readonly IQuotationAutoAssignStateReader _ownership = Substitute.For<IQuotationAutoAssignStateReader>();
    private readonly IWorkflowRelayService _relay = Substitute.For<IWorkflowRelayService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly IAssignmentEmailRepository _emails = Substitute.For<IAssignmentEmailRepository>();
    private readonly IAssignmentEmailCaseReader _reader = Substitute.For<IAssignmentEmailCaseReader>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly AppraisalAggregate _appraisal;
    private readonly List<AssignmentEmail> _added = [];

    public AssignAppraisalEmailTests()
    {
        _appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        _appraisal.AssignAdmin();
        _appraisals.GetByIdWithAllDataAsync(_appraisal.Id, Arg.Any<CancellationToken>()).Returns(_appraisal);
        _user.UserCode.Returns("P5229");
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(true);
        _ownership.WorkflowInstanceBelongsToAppraisalAsync(default, default, default).ReturnsForAnyArgs(true);
        _sender.Send(Arg.Any<GetFeePreviewQuery>(), Arg.Any<CancellationToken>())
            .Returns(AssignAppraisalQuotationGuardTests.FeePreviewWithBase(2_500m));
        _companies.GetByIdAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(_companyId, "Acme", null, IsAssignable: true));
        _emails.AddAsync(Arg.Do<AssignmentEmail>(_added.Add), Arg.Any<CancellationToken>());
        Documents(new(DocA, "deed.pdf", 1_000), new(DocB, "map.png", 2_000));
    }

    private void Documents(params AssignmentEmailDocument[] docs) =>
        _reader.GetDocumentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(c => docs.Where(d => c.Arg<IReadOnlyCollection<Guid>>().Contains(d.Id)).ToList());

    private Task AssignAsync(string decision, AssignmentEmailInput? email, bool? sendEmail, IConfiguration? configuration = null) =>
        new AssignAppraisalCommandHandler(
                _appraisals, _uow, _relay, Substitute.For<IQuotationRepository>(), _ownership, _sender, _user, _companies,
                _emails, _reader, configuration ?? new ConfigurationBuilder().Build())
            .Handle(new AssignAppraisalCommand(_appraisal.Id, Guid.NewGuid(), AssigneeCompanyId: _companyId.ToString(),
                DecisionTaken: decision, Remark: "หลัง 10 โมง", Email: email, SendEmail: sendEmail), Ct);

    private Task ResumedNever() =>
        _relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);

    [Theory]
    [InlineData("EXT")]
    [InlineData("EXTO")]
    public async Task Assign_and_send_writes_one_pending_email_for_the_assignment(string decision)
    {
        await AssignAsync(decision, Email(), sendEmail: true);

        var row = Assert.Single(_added);
        Assert.Equal(AssignmentEmailStatus.Pending, row.Status);
        Assert.Equal(_appraisal.Id, row.AppraisalId);
        Assert.Equal(_appraisal.Assignments.Single().Id, row.AssignmentId);
        Assert.Equal(["second@acme.co.th"], row.ExtraTo);
        Assert.Equal(["IntAdminAll", "Keyer", "boss@lhbank.co.th"], row.Cc);
        Assert.Equal("ขอนำส่งงานประเมิน", row.Subject);
        Assert.Equal("<p>เรียน <b>Acme</b></p>", row.BodyHtml);
        Assert.Equal([new AssignmentEmailAttachment(DocA, "request"), new AssignmentEmailAttachment(DocB, "upload")], row.Attachments);
    }

    [Fact]
    public async Task The_email_is_saved_with_the_assigner_and_remark_before_the_workflow_resumes()
    {
        await AssignAsync("EXT", Email(), sendEmail: true);

        Received.InOrder(() =>
        {
            _emails.AddAsync(Arg.Any<AssignmentEmail>(), Arg.Any<CancellationToken>());
            _uow.SaveChangesAsync(Arg.Any<CancellationToken>());
            _relay.ResumeWorkflowAsync(Arg.Any<Guid>(), "appraisal-assignment", Arg.Any<string>(),
                Arg.Any<Dictionary<string, object>>(), Arg.Any<IReadOnlyDictionary<string, WorkflowAssigneeOverride>?>(),
                Arg.Any<CancellationToken>());
        });
        Assert.Equal("P5229", _appraisal.Assignments.Single().AssignedBy);
    }

    [Fact]
    public async Task An_attachment_named_twice_is_attached_once()
    {
        await AssignAsync("EXT", Email(attachments: [new(DocA, "request"), new(DocA, "request")]), sendEmail: true);

        Assert.Equal([new AssignmentEmailAttachment(DocA, "request")], Assert.Single(_added).Attachments);
    }

    [Theory]
    [InlineData("EXT")]
    [InlineData("EXTO")]
    public async Task Assign_without_sending_writes_a_not_sent_row_with_no_content(string decision)
    {
        await AssignAsync(decision, Email(extraTo: ["not-an-email"]), sendEmail: false);

        var row = Assert.Single(_added);
        Assert.Equal(AssignmentEmailStatus.NotSent, row.Status);
        Assert.Equal(_appraisal.Assignments.Single().Id, row.AssignmentId);
        Assert.Empty(row.ExtraTo);
        Assert.Empty(row.Subject);
        Assert.Empty(row.Attachments);
    }

    [Fact]
    public async Task Without_an_admin_choice_no_row_is_written()
    {
        await AssignAsync("EXT", Email(), sendEmail: null);

        Assert.Empty(_added);
    }

    [Theory]
    [InlineData("INT")]
    [InlineData("C")]
    [InlineData("R")]
    public async Task Other_decisions_write_no_email(string decision)
    {
        await AssignAsync(decision, Email(), sendEmail: true);

        Assert.Empty(_added);
    }

    [Fact]
    public async Task An_attachment_that_is_not_a_live_document_assigns_nothing()
    {
        Documents(new AssignmentEmailDocument(DocA, "deed.pdf", 1_000));

        await Assert.ThrowsAsync<BadRequestException>(() => AssignAsync("EXT", Email(), sendEmail: true));

        Assert.Empty(_added);
        Assert.NotEqual("P5229", _appraisal.Assignments.Single().AssignedBy);
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ResumedNever();
    }

    [Fact]
    public async Task Attachments_over_the_size_limit_assign_nothing_and_name_the_limit()
    {
        Documents(new(DocA, "deed.pdf", 6 * 1024 * 1024), new(DocB, "map.png", 5 * 1024 * 1024));

        var error = await Assert.ThrowsAsync<BadRequestException>(() => AssignAsync("EXTO", Email(), sendEmail: true));

        Assert.Contains("10 MB", error.Message);
        Assert.Empty(_added);
        await ResumedNever();
    }

    [Fact]
    public async Task The_size_limit_comes_from_mail_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mail:MaxAttachmentBytes"] = "2500" })
            .Build();

        await Assert.ThrowsAsync<BadRequestException>(() => AssignAsync("EXT", Email(), sendEmail: true, configuration));
        await ResumedNever();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Only_an_appraisal_administrator_can_choose_the_email(bool sendEmail)
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(false);

        await Assert.ThrowsAsync<ForbiddenException>(() => AssignAsync("EXT", Email(), sendEmail));

        Assert.Empty(_added);
        Assert.NotEqual("P5229", _appraisal.Assignments.Single().AssignedBy);
        await ResumedNever();
    }

    [Fact]
    public async Task Without_an_email_choice_no_permission_is_asked()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(false);

        await AssignAsync("EXT", null, sendEmail: null);

        Assert.Equal("P5229", _appraisal.Assignments.Single().AssignedBy);
    }

    private void ResumeFails() =>
        _relay.ResumeWorkflowAsync(default, default!, default!, default!, default, default)
            .ReturnsForAnyArgs<Task>(_ => throw new BadRequestException("No company could be selected."));

    [Fact]
    public async Task A_failed_resume_removes_the_email_it_wrote_and_reports_the_original_error()
    {
        ResumeFails();

        var error = await Assert.ThrowsAsync<BadRequestException>(() => AssignAsync("EXT", Email(), sendEmail: true));

        Assert.Equal("No company could be selected.", error.Message);
        await _emails.Received(1).DeleteNowAsync(Assert.Single(_added).Id, CancellationToken.None);
    }

    [Fact]
    public async Task The_admin_sees_the_workflow_error_even_when_the_cleanup_fails()
    {
        ResumeFails();
        _emails.DeleteNowAsync(default, default).ReturnsForAnyArgs<Task>(_ => throw new InvalidOperationException("SQL down"));

        var error = await Assert.ThrowsAsync<BadRequestException>(() => AssignAsync("EXT", Email(), sendEmail: true));

        Assert.Equal("No company could be selected.", error.Message);
    }

    [Fact]
    public async Task A_resume_that_stands_keeps_the_email()
    {
        await AssignAsync("EXTO", Email(), sendEmail: false);

        Assert.Single(_added);
        await _emails.DidNotReceiveWithAnyArgs().DeleteNowAsync(default, default);
    }

    [Fact]
    public async Task A_failed_resume_without_an_email_deletes_nothing()
    {
        ResumeFails();

        await Assert.ThrowsAsync<BadRequestException>(() => AssignAsync("INT", Email(), sendEmail: true));

        await _emails.DidNotReceiveWithAnyArgs().DeleteNowAsync(default, default);
    }
}

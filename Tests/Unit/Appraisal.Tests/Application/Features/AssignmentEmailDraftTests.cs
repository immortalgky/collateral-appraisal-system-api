using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Appraisal.Infrastructure;
using Appraisal.Infrastructure.Repositories;
using Auth.Contracts.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;

namespace Appraisal.Tests.Application.Features;

/// <summary>Drives the assignment email draft through its GET query and its save command.</summary>
public class AssignmentEmailDraftTests
{
    private static readonly Guid AppraisalId = Guid.NewGuid();
    private static readonly Guid AssignmentId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private readonly AppraisalDbContext _db = new(
        new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly IAssignmentEmailCaseReader _reader = Substitute.For<IAssignmentEmailCaseReader>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private IConfiguration _configuration = new ConfigurationBuilder().Build();

    public AssignmentEmailDraftTests()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(true);
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, "jobs@acme.co.th", "บริษัท แอคมี จำกัด"));
        FeeTotal(2675m);
    }

    private void FeeTotal(decimal total) =>
        _sender.Send(Arg.Is<GetFeePreviewQuery>(q => q.AppraisalId == AppraisalId), Arg.Any<CancellationToken>())
            .Returns(new FeePreviewResult(null, null, new FeeBaseSourceDto(FeeBaseKinds.None, null, null, null, null, null, null),
                null, [], true, total / 1.07m, 7m, total - total / 1.07m, total));

    private static AssignmentEmailCase Exto(string? keyerName = "สมชาย ใจดี") => new(
        AppraisalNumber: "69042630",
        CustomerName: "นายสมศักดิ์ มั่นคง",
        ContactName: "คุณวิไล",
        ContactPhone: "081-234-5678",
        FeePaymentText: "ชำระที่ธนาคารฯ (ก่อนวันประเมิน)",
        AssignmentId: AssignmentId,
        AssignmentMethod: "Offline",
        AssigneeCompanyId: CompanyId.ToString(),
        KeyerName: keyerName,
        FollowupMethod: null,
        FollowupName: null,
        Remark: "เข้าสำรวจได้หลัง 10 โมง");

    private Task<AssignmentEmailDraftResult> GetAsync(AssignmentEmailCase @case)
    {
        _reader.GetCaseAsync(AppraisalId, Arg.Any<CancellationToken>()).Returns(@case);
        return new GetAssignmentEmailDraftQueryHandler(_db, _reader, _companies, _sender, _user, _configuration)
            .Handle(new GetAssignmentEmailDraftQuery(AppraisalId), CancellationToken.None);
    }

    [Fact]
    public async Task Exto_default_draft_is_the_bank_template_filled_from_the_case()
    {
        var draft = await GetAsync(Exto());

        Assert.Null(draft.Id);
        Assert.Equal("jobs@acme.co.th", draft.CompanyTo);
        Assert.Empty(draft.ExtraTo);
        Assert.Equal(["IntAdminAll", "Keyer", "KeyerSupervisor"], draft.Cc);
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ มั่นคง 69042630", draft.Subject);
        Assert.Equal(
            "<p>เรียน บริษัท แอคมี จำกัด</p>" +
            "<p>ขอนำส่งงานประเมิน ลูกค้าจำนวน 1 รายการ (หากได้วันเข้าสำรวจแล้ว โปรดแจ้งกลับ)</p>" +
            "<p>App No. : 69042630<br>" +
            "ชื่อลูกค้า : นายสมศักดิ์ มั่นคง<br>" +
            "ชื่อผู้ติดต่อ : คุณวิไล<br>" +
            "โทรศัพท์ : 081-234-5678<br>" +
            "ค่าธรรมเนียม : 2,675.00<br>" +
            "การชำระค่าธรรมเนียม : ชำระที่ธนาคารฯ (ก่อนวันประเมิน)<br>" +
            "ผู้รับผิดชอบ : สมชาย ใจดี<br>" +
            "หมายเหตุ : เข้าสำรวจได้หลัง 10 โมง</p>" +
            "<p>จึงเรียนมาเพื่อทราบและดำเนินการต่อ</p>",
            draft.BodyHtml);
        Assert.Empty(draft.Attachments);
        Assert.Equal(10, draft.MaxAttachments);
        Assert.Equal(10L * 1024 * 1024, draft.MaxAttachmentBytes);
    }

    [Fact]
    public async Task Ext_with_automatic_company_and_followup_keeps_both_placeholders()
    {
        var draft = await GetAsync(Exto() with
        {
            AssignmentMethod = "RoundRobin", AssigneeCompanyId = null, KeyerName = null,
            FollowupMethod = "RoundRobin", FollowupName = "ไม่ควรถูกใช้"
        });

        Assert.Null(draft.CompanyTo);
        Assert.Equal(["IntAdminAll", "FollowupStaff", "FollowupSupervisor"], draft.Cc);
        Assert.StartsWith("<p>เรียน {{company}}</p>", draft.BodyHtml);
        Assert.Contains("ผู้รับผิดชอบ : {{responsible}}<br>", draft.BodyHtml);
    }

    [Fact]
    public async Task Ext_with_a_hand_picked_followup_names_that_officer()
    {
        var draft = await GetAsync(Exto() with
        {
            AssignmentMethod = "Manual", KeyerName = null, FollowupMethod = "Manual", FollowupName = "วิชัย รักงาน"
        });

        Assert.Equal("jobs@acme.co.th", draft.CompanyTo);
        Assert.Contains("ผู้รับผิดชอบ : วิชัย รักงาน<br>", draft.BodyHtml);
    }

    [Fact]
    public async Task Exto_with_an_automatic_keyer_keeps_the_responsible_placeholder()
    {
        var draft = await GetAsync(Exto(keyerName: null));

        Assert.Contains("ผู้รับผิดชอบ : {{responsible}}<br>", draft.BodyHtml);
    }

    [Fact]
    public async Task Case_values_are_html_encoded_and_blank_ones_read_as_a_dash()
    {
        var draft = await GetAsync(Exto() with { CustomerName = "<b>A&B</b>", ContactPhone = null, Remark = " " });

        Assert.Contains("ชื่อลูกค้า : &lt;b&gt;A&amp;B&lt;/b&gt;<br>", draft.BodyHtml);
        Assert.Contains("โทรศัพท์ : -<br>", draft.BodyHtml);
        Assert.Contains("หมายเหตุ : -</p>", draft.BodyHtml);
    }

    [Fact]
    public async Task Non_admin_cannot_read_the_draft()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(false);

        await Assert.ThrowsAsync<ForbiddenException>(() => GetAsync(Exto()));
    }

    // ── save ──────────────────────────────────────────────────────────────────

    private static readonly Guid DocA = Guid.NewGuid();
    private static readonly Guid DocB = Guid.NewGuid();

    private void CaseExists() =>
        _reader.GetCaseAsync(AppraisalId, Arg.Any<CancellationToken>()).Returns(Exto());

    private void Documents(params AssignmentEmailDocument[] docs) =>
        _reader.GetDocumentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(c => docs.Where(d => c.Arg<IReadOnlyCollection<Guid>>().Contains(d.Id)).ToList());

    private static SaveAssignmentEmailDraftCommand Save(
        List<SaveAssignmentEmailAttachment>? attachments = null,
        List<string>? extraTo = null,
        List<string>? cc = null) =>
        new(AppraisalId,
            extraTo ?? ["second@acme.co.th"],
            cc ?? ["IntAdminAll", "Keyer", "boss@lhbank.co.th"],
            "ขอนำส่งงานประเมิน",
            "<p>เรียน <b>Acme</b><script>x</script></p>",
            attachments ?? [new(DocA, "request"), new(DocB, "upload")]);

    private async Task SaveAsync(SaveAssignmentEmailDraftCommand command)
    {
        await new SaveAssignmentEmailDraftCommandHandler(
                new AssignmentEmailRepository(_db), _reader, _user, _configuration)
            .Handle(command, CancellationToken.None);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Saved_draft_is_returned_as_saved_with_its_attachments()
    {
        CaseExists();
        Documents(new(DocA, "deed.pdf", 1_000), new(DocB, "map.png", 2_000));

        await SaveAsync(Save());
        var draft = await GetAsync(Exto());

        Assert.NotNull(draft.Id);
        Assert.Equal("jobs@acme.co.th", draft.CompanyTo);
        Assert.Equal(["second@acme.co.th"], draft.ExtraTo);
        Assert.Equal(["IntAdminAll", "Keyer", "boss@lhbank.co.th"], draft.Cc);
        Assert.Equal("ขอนำส่งงานประเมิน", draft.Subject);
        Assert.Equal("<p>เรียน <b>Acme</b><script>x</script></p>", draft.BodyHtml);
        Assert.Equal(
            [new AssignmentEmailAttachmentDto(DocA, "request", "deed.pdf", 1_000),
             new AssignmentEmailAttachmentDto(DocB, "upload", "map.png", 2_000)],
            draft.Attachments.OrderBy(a => a.FileName));
        var row = await _db.AssignmentEmails.SingleAsync();
        Assert.Equal(AssignmentId, row.AssignmentId);
        Assert.Equal("Draft", row.Status);
    }

    [Fact]
    public async Task Restoring_the_template_ignores_the_saved_draft()
    {
        CaseExists();
        Documents(new(DocA, "deed.pdf", 1_000), new(DocB, "map.png", 2_000));
        await SaveAsync(Save());

        var draft = await new GetAssignmentEmailDraftQueryHandler(_db, _reader, _companies, _sender, _user, _configuration)
            .Handle(new GetAssignmentEmailDraftQuery(AppraisalId, Template: true), CancellationToken.None);

        Assert.Null(draft.Id);
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ มั่นคง 69042630", draft.Subject);
        Assert.Empty(draft.Attachments);
    }

    [Fact]
    public async Task Saving_again_updates_the_one_draft()
    {
        CaseExists();
        Documents(new(DocA, "deed.pdf", 1_000), new(DocB, "map.png", 2_000));

        await SaveAsync(Save());
        await SaveAsync(Save(attachments: [new(DocA, "appraisal")], extraTo: [], cc: ["IntAdminAll"]));

        var row = await _db.AssignmentEmails.SingleAsync();
        Assert.Empty(row.ExtraTo);
        Assert.Equal(["IntAdminAll"], row.Cc);
        Assert.Equal([new AssignmentEmailAttachment(DocA, "appraisal")], row.Attachments);
    }

    [Fact]
    public async Task Attachments_over_the_size_limit_are_rejected_with_the_limit_in_the_message()
    {
        CaseExists();
        Documents(new(DocA, "deed.pdf", 6 * 1024 * 1024), new(DocB, "map.png", 5 * 1024 * 1024));

        var error = await Assert.ThrowsAsync<BadRequestException>(() => SaveAsync(Save()));

        Assert.Contains("10 MB", error.Message);
        Assert.Empty(_db.AssignmentEmails);
    }

    [Fact]
    public async Task The_size_limit_comes_from_mail_configuration()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mail:MaxAttachmentBytes"] = "2500" })
            .Build();
        CaseExists();
        Documents(new(DocA, "deed.pdf", 1_000), new(DocB, "map.png", 2_000));

        await Assert.ThrowsAsync<BadRequestException>(() => SaveAsync(Save()));
        Assert.Equal(2500, (await GetAsync(Exto())).MaxAttachmentBytes);
    }

    [Fact]
    public async Task An_attachment_that_is_not_a_live_document_is_rejected()
    {
        CaseExists();
        Documents(new AssignmentEmailDocument(DocA, "deed.pdf", 1_000));

        await Assert.ThrowsAsync<BadRequestException>(() => SaveAsync(Save()));
    }

    [Fact]
    public async Task Non_admin_cannot_save_the_draft()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(false);
        CaseExists();

        await Assert.ThrowsAsync<ForbiddenException>(() => SaveAsync(Save()));
    }

    [Fact]
    public async Task Saving_for_an_unknown_appraisal_is_not_found() =>
        await Assert.ThrowsAsync<AppraisalNotFoundException>(() => SaveAsync(Save(attachments: [])));

    // ── validation ────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> Errors(SaveAssignmentEmailDraftCommand command) =>
        new SaveAssignmentEmailDraftCommandValidator().Validate(command).Errors.Select(e => e.ErrorMessage).ToList();

    [Fact]
    public void A_well_formed_draft_passes_validation() => Assert.Empty(Errors(Save()));

    [Fact]
    public void More_than_ten_attachments_are_rejected()
    {
        var errors = Errors(Save(attachments: Enumerable.Range(0, 11)
            .Select(_ => new SaveAssignmentEmailAttachment(Guid.NewGuid(), "upload")).ToList()));

        Assert.Contains("At most 10 attachments can be sent.", errors);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Acme <jobs@acme.co.th>")]
    [InlineData("a@b.co;c@d.co")]
    public void A_malformed_extra_to_address_is_rejected(string address) =>
        Assert.NotEmpty(Errors(Save(extraTo: [address])));

    [Fact]
    public void An_address_longer_than_254_characters_is_rejected() =>
        Assert.NotEmpty(Errors(Save(cc: [new string('a', 250) + "@x.co"])));

    [Fact]
    public void Cc_takes_role_tokens_and_addresses_but_not_unknown_tokens() =>
        Assert.NotEmpty(Errors(Save(cc: ["IntAdminAll", "Everyone"])));

    [Fact]
    public void An_unknown_attachment_source_is_rejected() =>
        Assert.NotEmpty(Errors(Save(attachments: [new(DocA, "elsewhere")])));

    [Fact]
    public void A_blank_subject_is_rejected() =>
        Assert.NotEmpty(Errors(Save() with { Subject = " " }));
}

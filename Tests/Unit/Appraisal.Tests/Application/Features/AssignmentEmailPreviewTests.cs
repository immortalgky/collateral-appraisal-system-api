using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals.Exceptions;
using Auth.Contracts.Companies;
using Auth.Contracts.Users;
using MediatR;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;

namespace Appraisal.Tests.Application.Features;

/// <summary>The email the admin sees when pressing มอบหมาย: the bank template filled from the case.</summary>
public class AssignmentEmailPreviewTests
{
    private static readonly Guid AppraisalId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private readonly IAssignmentEmailCaseReader _reader = Substitute.For<IAssignmentEmailCaseReader>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly IUserLookupService _users = Substitute.For<IUserLookupService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private IConfiguration _configuration = new ConfigurationBuilder().Build();

    public AssignmentEmailPreviewTests()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(true);
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, "jobs@acme.co.th", "บริษัท แอคมี จำกัด"));
        FeeTotal(2675m);
        _users.GetEmailsInRoleAsync("IntAdmin", Arg.Any<CancellationToken>()).Returns(["admin1@bank.co.th", "Admin2@bank.co.th"]);
        _users.GetEmailsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns([]);
        _users.GetSupervisorEmailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _users.GetNamesByEmailAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin1@bank.co.th"] = "สมหญิง แอดมิน", ["staff@bank.co.th"] = "วิชัย รักงาน", ["boss@bank.co.th"] = "boss01"
            });
    }

    private void FeeTotal(decimal total) =>
        _sender.Send(Arg.Is<GetFeePreviewQuery>(q => q.AppraisalId == AppraisalId), Arg.Any<CancellationToken>())
            .Returns(new FeePreviewResult(null, null, new FeeBaseSourceDto(FeeBaseKinds.None, null, null, null, null, null, null),
                null, [new FeePreviewItemDto(null, "01", "Appraisal fee", total / 1.07m, "System")],
                true, total / 1.07m, 7m, total - total / 1.07m, total));

    private void NoFee() =>
        _sender.Send(Arg.Is<GetFeePreviewQuery>(q => q.AppraisalId == AppraisalId), Arg.Any<CancellationToken>())
            .Returns(new FeePreviewResult(null, null, new FeeBaseSourceDto(FeeBaseKinds.None, null, null, null, null, null, null),
                null, [], false, 0m, 7m, 0m, 0m));

    private static AssignmentEmailCase Exto(string? keyerName = "สมชาย ใจดี") => new(
        AppraisalNumber: "69042630",
        CustomerName: "นายสมศักดิ์ มั่นคง",
        ContactName: "คุณวิไล",
        ContactPhone: "081-234-5678",
        FeePaymentText: "ชำระที่ธนาคารฯ (ก่อนวันประเมิน)",
        AssignmentMethod: "Offline",
        AssigneeCompanyId: CompanyId.ToString(),
        KeyerName: keyerName,
        FollowupMethod: null,
        FollowupName: null,
        Remark: "เข้าสำรวจได้หลัง 10 โมง");

    private Task<AssignmentEmailPreviewResult> GetAsync(AssignmentEmailCase @case)
    {
        _reader.GetCaseAsync(AppraisalId, Arg.Any<CancellationToken>()).Returns(@case);
        return Preview(new GetAssignmentEmailPreviewQuery(AppraisalId));
    }

    private Task<AssignmentEmailPreviewResult> Preview(GetAssignmentEmailPreviewQuery query) =>
        new GetAssignmentEmailPreviewQueryHandler(
            new AssignmentEmailComposer(_reader, _companies, _users, _sender, _configuration), _user, _sender)
            .Handle(query, CancellationToken.None);

    private static readonly Guid StoredEmailId = Guid.NewGuid();

    private void Stored(params string[] cc) =>
        _sender.Send(Arg.Is<GetAssignmentEmailSentQuery>(q => q.AppraisalId == AppraisalId && q.EmailId == StoredEmailId),
                Arg.Any<CancellationToken>())
            .Returns(new AssignmentEmailSentResult(
                StoredEmailId, false, "Sent", "P5229", null, null, [], [], ["extra@acme.co.th"], cc.ToList(),
                "stored subject", "<p>stored body</p>", [new AssignmentEmailAttachmentDto(Guid.NewGuid(), "request", "deed.pdf", 10)]));

    [Fact]
    public async Task A_stored_email_comes_back_with_its_roles_expanded_and_hand_typed_addresses_kept()
    {
        _reader.GetCaseAsync(AppraisalId, Arg.Any<CancellationToken>()).Returns(Exto() with { KeyerUserId = "K0001" });
        _users.GetEmailsAsync(Arg.Is<IEnumerable<string>>(u => u.SequenceEqual(new[] { "K0001" })), Arg.Any<CancellationToken>())
            .Returns(["staff@bank.co.th"]);
        _users.GetSupervisorEmailsAsync("K0001", Arg.Any<CancellationToken>()).Returns(["boss@bank.co.th"]);
        Stored("IntAdminAll", "Keyer", "KeyerSupervisor", "x@y.co");

        var draft = await Preview(new GetAssignmentEmailPreviewQuery(AppraisalId, StoredEmailId));

        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "staff@bank.co.th", "boss@bank.co.th", "x@y.co"], draft.Cc);
        Assert.Equal(
            [new AssignmentEmailPerson("admin1@bank.co.th", "สมหญิง แอดมิน"), new AssignmentEmailPerson("Admin2@bank.co.th", null),
             new AssignmentEmailPerson("staff@bank.co.th", "วิชัย รักงาน"), new AssignmentEmailPerson("boss@bank.co.th", "boss01"),
             new AssignmentEmailPerson("x@y.co", null)],
            draft.CcPeople);
        Assert.Equal("stored subject", draft.Subject);
        Assert.Equal("<p>stored body</p>", draft.BodyHtml);
        Assert.Equal(["extra@acme.co.th"], draft.ExtraTo);
        Assert.Equal("deed.pdf", Assert.Single(draft.Attachments).FileName);
        Assert.Equal("บริษัท แอคมี จำกัด", draft.CompanyName);
        Assert.Equal("jobs@acme.co.th", draft.CompanyTo);
    }

    [Fact]
    public async Task A_stored_email_on_a_row_with_no_keyer_keeps_the_keyer_roles_as_tokens()
    {
        _reader.GetCaseAsync(AppraisalId, Arg.Any<CancellationToken>()).Returns(Exto());
        Stored("IntAdminAll", "Keyer", "KeyerSupervisor", "x@y.co");

        var draft = await Preview(new GetAssignmentEmailPreviewQuery(AppraisalId, StoredEmailId));

        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "Keyer", "KeyerSupervisor", "x@y.co"], draft.Cc);
    }

    [Fact]
    public async Task A_stored_email_of_another_appraisal_is_a_404()
    {
        _reader.GetCaseAsync(AppraisalId, Arg.Any<CancellationToken>()).Returns(Exto());
        _sender.Send(Arg.Any<GetAssignmentEmailSentQuery>(), Arg.Any<CancellationToken>())
            .Returns<AssignmentEmailSentResult>(_ => throw new NotFoundException("AssignmentEmail", StoredEmailId));

        await Assert.ThrowsAsync<NotFoundException>(() => Preview(new GetAssignmentEmailPreviewQuery(AppraisalId, StoredEmailId)));
    }

    [Fact]
    public async Task Exto_preview_is_the_bank_template_filled_from_the_case()
    {
        var draft = await GetAsync(Exto());

        Assert.Equal("jobs@acme.co.th", draft.CompanyTo);
        Assert.Empty(draft.ExtraTo);
        Assert.Equal("บริษัท แอคมี จำกัด", draft.CompanyName);
        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "Keyer", "KeyerSupervisor"], draft.Cc);
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ มั่นคง 69042630", draft.Subject);
        Assert.Equal(
            "<p>เรียน บริษัท แอคมี จำกัด<br><br>" +
            "ขอนำส่งงานประเมิน ลูกค้าจำนวน 1 รายการ (หากได้วันเข้าสำรวจแล้ว โปรดแจ้งกลับ)<br>" +
            "หมายเลขประเมิน: 69042630<br>" +
            "ชื่อลูกค้า: นายสมศักดิ์ มั่นคง<br>" +
            "ชื่อผู้ติดต่อ: คุณวิไล<br>" +
            "โทรศัพท์: 081-234-5678<br>" +
            "ค่าธรรมเนียม: 2,675.00<br>" +
            "การชำระค่าธรรมเนียม: ชำระที่ธนาคารฯ (ก่อนวันประเมิน)<br>" +
            "ผู้รับผิดชอบ: สมชาย ใจดี<br>" +
            "หมายเหตุ: เข้าสำรวจได้หลัง 10 โมง<br><br>" +
            "จึงเรียนมาเพื่อทราบและดำเนินการต่อ</p>",
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
            FollowupMethod = "RoundRobin", FollowupName = null, FollowupUserId = null
        });

        Assert.Null(draft.CompanyName);
        Assert.Null(draft.CompanyTo);
        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "FollowupStaff", "FollowupSupervisor"], draft.Cc);
        Assert.StartsWith("<p>เรียน {{company}}<br><br>", draft.BodyHtml);
        Assert.Contains("ผู้รับผิดชอบ: {{responsible}}<br>", draft.BodyHtml);
    }

    [Fact]
    public async Task Ext_with_a_hand_picked_followup_names_that_officer()
    {
        var draft = await GetAsync(Exto() with
        {
            AssignmentMethod = "Manual", KeyerName = null, FollowupMethod = "Manual", FollowupName = "วิชัย รักงาน"
        });

        Assert.Equal("jobs@acme.co.th", draft.CompanyTo);
        Assert.Contains("ผู้รับผิดชอบ: วิชัย รักงาน<br>", draft.BodyHtml);
    }

    [Fact]
    public async Task Exto_with_an_automatic_keyer_keeps_the_responsible_placeholder()
    {
        var draft = await GetAsync(Exto(keyerName: null));

        Assert.Contains("ผู้รับผิดชอบ: {{responsible}}<br>", draft.BodyHtml);
    }

    [Fact]
    public async Task Case_values_are_html_encoded_and_blank_ones_read_as_a_dash()
    {
        var draft = await GetAsync(Exto() with { CustomerName = "<b>A&B</b>", ContactPhone = null, Remark = " " });

        Assert.Contains("ชื่อลูกค้า: &lt;b&gt;A&amp;B&lt;/b&gt;<br>", draft.BodyHtml);
        Assert.Contains("โทรศัพท์: -<br>", draft.BodyHtml);
        Assert.Contains("หมายเหตุ: -<br><br>จึงเรียน", draft.BodyHtml);
    }

    [Fact]
    public async Task A_case_with_no_fee_shows_a_dash_not_a_zero_fee()
    {
        NoFee();

        var draft = await GetAsync(Exto());

        Assert.Contains("ค่าธรรมเนียม: -<br>", draft.BodyHtml);
    }

    [Fact]
    public async Task A_fee_with_nothing_billable_yet_shows_a_dash()
    {
        _sender.Send(Arg.Is<GetFeePreviewQuery>(q => q.AppraisalId == AppraisalId), Arg.Any<CancellationToken>())
            .Returns(new FeePreviewResult(null, null, new FeeBaseSourceDto(FeeBaseKinds.None, null, null, null, null, null, null),
                null, [new FeePreviewItemDto(Guid.NewGuid(), "01", "Appraisal fee", 2500m, "User")],
                HasBillableBase: false, 0m, 7m, 0m, 0m));

        var draft = await GetAsync(Exto());

        Assert.Contains("ค่าธรรมเนียม: -<br>", draft.BodyHtml);
    }

    [Fact]
    public async Task The_template_subject_is_one_line_whatever_the_customer_name_holds()
    {
        var draft = await GetAsync(Exto() with { CustomerName = "นายสมศักดิ์\r\n  มั่นคง" });

        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ มั่นคง 69042630", draft.Subject);
    }

    [Fact]
    public async Task The_template_subject_drops_control_characters_so_it_can_be_sent()
    {
        var draft = await GetAsync(Exto() with { CustomerName = "นายสมศักดิ์\u0000\u001b มั่นคง" });

        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ มั่นคง 69042630", draft.Subject);
    }

    [Fact]
    public async Task A_company_with_a_blank_thai_name_is_greeted_by_its_name()
    {
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, "jobs@acme.co.th", ""));

        var draft = await GetAsync(Exto());

        Assert.StartsWith("<p>เรียน Acme Appraisal<br><br>", draft.BodyHtml);
    }

    [Fact]
    public async Task Offline_method_is_recognised_whatever_its_casing()
    {
        var draft = await GetAsync(Exto() with { AssignmentMethod = "offline" });

        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "Keyer", "KeyerSupervisor"], draft.Cc);
    }

    [Fact]
    public async Task Admins_are_listed_with_their_names_and_an_unnamed_one_has_none()
    {
        var draft = await GetAsync(Exto());

        Assert.Equal(
            [new AssignmentEmailPerson("admin1@bank.co.th", "สมหญิง แอดมิน"), new AssignmentEmailPerson("Admin2@bank.co.th", null)],
            draft.CcPeople);
    }

    [Fact]
    public async Task Whoever_the_row_holds_is_shown_whatever_the_method_and_status()
    {
        _users.GetEmailsAsync(Arg.Is<IEnumerable<string>>(u => u.SequenceEqual(new[] { "F0001" })), Arg.Any<CancellationToken>())
            .Returns(["staff@bank.co.th"]);
        _users.GetEmailsAsync(Arg.Is<IEnumerable<string>>(u => u.SequenceEqual(new[] { "K0001" })), Arg.Any<CancellationToken>())
            .Returns(["keyer@bank.co.th"]);

        // EXT, automatic follow-up the system already resolved, on a row that is not Pending.
        var ext = await GetAsync(Exto() with
        {
            AssignmentMethod = "RoundRobin", FollowupMethod = "RoundRobin", FollowupUserId = "F0001", FollowupName = "วิชัย รักงาน",
            AssignmentStatus = "InProgress"
        });
        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "staff@bank.co.th", "FollowupSupervisor"], ext.Cc);
        Assert.Contains("ผู้รับผิดชอบ: วิชัย รักงาน<br>", ext.BodyHtml);

        // EXTO with the keyer set.
        var exto = await GetAsync(Exto() with { KeyerUserId = "K0001" });
        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "keyer@bank.co.th", "KeyerSupervisor"], exto.Cc);
        Assert.Contains("ผู้รับผิดชอบ: สมชาย ใจดี<br>", exto.BodyHtml);
    }

    [Fact]
    public async Task Nobody_on_the_row_keeps_the_tokens_and_the_placeholder()
    {
        var exto = await GetAsync(Exto(keyerName: null));
        var ext = await GetAsync(Exto() with { AssignmentMethod = "Manual", KeyerName = null, FollowupMethod = "RoundRobin" });

        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "Keyer", "KeyerSupervisor"], exto.Cc);
        Assert.Contains("ผู้รับผิดชอบ: {{responsible}}<br>", exto.BodyHtml);
        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "FollowupStaff", "FollowupSupervisor"], ext.Cc);
        Assert.Contains("ผู้รับผิดชอบ: {{responsible}}<br>", ext.BodyHtml);
        await _users.DidNotReceive().GetEmailsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_hand_picked_followup_expands_the_staff_and_supervisor_and_drops_duplicates()
    {
        _users.GetEmailsAsync(Arg.Is<IEnumerable<string>>(u => u.SequenceEqual(new[] { "F0001" })), Arg.Any<CancellationToken>())
            .Returns(["staff@bank.co.th"]);
        _users.GetSupervisorEmailsAsync("F0001", Arg.Any<CancellationToken>())
            .Returns(["BOSS@bank.co.th", "ADMIN1@bank.co.th", "boss@bank.co.th"]);

        var draft = await GetAsync(Exto() with
        {
            AssignmentMethod = "Manual", FollowupMethod = "Manual", FollowupUserId = "F0001", FollowupName = "วิชัย รักงาน"
        });

        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "staff@bank.co.th", "BOSS@bank.co.th"], draft.Cc);
        Assert.Equal(["admin1@bank.co.th", "Admin2@bank.co.th", "staff@bank.co.th", "BOSS@bank.co.th"],
            draft.CcPeople.Select(p => p.Email));
        Assert.Equal("วิชัย รักงาน", draft.CcPeople[2].Name);
        Assert.Equal("boss01", draft.CcPeople[3].Name);
    }

    [Fact]
    public async Task A_known_role_with_nobody_usable_keeps_its_token()
    {
        _users.GetEmailsInRoleAsync("IntAdmin", Arg.Any<CancellationToken>()).Returns([]);

        var draft = await GetAsync(Exto());

        Assert.Equal(["IntAdminAll", "Keyer", "KeyerSupervisor"], draft.Cc);
        Assert.Empty(draft.CcPeople);
    }

    [Fact]
    public async Task A_stored_email_holding_several_addresses_becomes_one_chip_per_plain_address_and_keeps_the_name()
    {
        _users.GetEmailsInRoleAsync("IntAdmin", Arg.Any<CancellationToken>())
            .Returns(["a@x.co; B@x.co, not an address", "b@x.co", ";"]);
        _users.GetNamesByEmailAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["a@x.co; B@x.co, not an address"] = "สมหญิง แอดมิน" });

        var draft = await GetAsync(Exto());

        Assert.Equal(["a@x.co", "B@x.co", "Keyer", "KeyerSupervisor"], draft.Cc);
        Assert.Equal([new AssignmentEmailPerson("a@x.co", "สมหญิง แอดมิน"), new AssignmentEmailPerson("B@x.co", "สมหญิง แอดมิน")],
            draft.CcPeople);
        var validator = new AssignmentEmailInputValidator();
        Assert.True(validator.Validate(new AssignmentEmailInput([], draft.Cc, "s", "<p>b</p>", [])).IsValid);
    }

    [Fact]
    public async Task A_stored_email_with_a_display_name_keeps_its_bare_address()
    {
        _users.GetEmailsInRoleAsync("IntAdmin", Arg.Any<CancellationToken>())
            .Returns(["Somchai <somchai@bank.co.th>", "somchai@BANK.co.th", "ฝ่ายประเมิน"]);

        var draft = await GetAsync(Exto());

        Assert.Equal(["somchai@bank.co.th", "Keyer", "KeyerSupervisor"], draft.Cc);
    }

    [Fact]
    public async Task A_messy_stored_email_is_dropped_not_rewritten_into_another_address()
    {
        _users.GetEmailsInRoleAsync("IntAdmin", Arg.Any<CancellationToken>())
            .Returns(["john smith@bank.co.th", "info@x.co sales@x.co", "Somchai <s@bank.co.th>", "plain@bank.co.th"]);

        var draft = await GetAsync(Exto());

        Assert.Equal(["s@bank.co.th", "plain@bank.co.th", "Keyer", "KeyerSupervisor"], draft.Cc);
    }

    [Theory]
    [InlineData(";", null)]
    [InlineData(" , ", null)]
    [InlineData("jobs@acme.co.th; junk", "jobs@acme.co.th")]
    [InlineData("a@acme.co.th;A@ACME.co.th, b@acme.co.th", "a@acme.co.th; b@acme.co.th")]
    public async Task The_company_address_is_the_plain_addresses_left_after_splitting(string stored, string? expected)
    {
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, stored, "บริษัท แอคมี จำกัด"));

        var draft = await GetAsync(Exto());

        Assert.Equal("บริษัท แอคมี จำกัด", draft.CompanyName);
        Assert.Equal(expected, draft.CompanyTo);
    }

    [Fact]
    public async Task A_company_without_an_email_is_named_but_has_no_to_address()
    {
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, "  ", "บริษัท แอคมี จำกัด"));

        var draft = await GetAsync(Exto());

        Assert.Equal("บริษัท แอคมี จำกัด", draft.CompanyName);
        Assert.Null(draft.CompanyTo);
    }

    [Fact]
    public async Task The_size_limit_comes_from_mail_configuration()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mail:MaxAttachmentBytes"] = "2500" })
            .Build();

        Assert.Equal(2500, (await GetAsync(Exto())).MaxAttachmentBytes);
    }

    [Fact]
    public async Task Non_admin_cannot_read_the_preview()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(false);

        await Assert.ThrowsAsync<ForbiddenException>(() => GetAsync(Exto()));
    }
}

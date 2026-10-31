using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals.Exceptions;
using Auth.Contracts.Companies;
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
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private IConfiguration _configuration = new ConfigurationBuilder().Build();

    public AssignmentEmailPreviewTests()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(true);
        _companies.GetByIdAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(CompanyId, "Acme Appraisal", null, true, "jobs@acme.co.th", "บริษัท แอคมี จำกัด"));
        FeeTotal(2675m);
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
        return new GetAssignmentEmailPreviewQueryHandler(new AssignmentEmailComposer(_reader, _companies, _sender, _configuration), _user)
            .Handle(new GetAssignmentEmailPreviewQuery(AppraisalId), CancellationToken.None);
    }

    [Fact]
    public async Task Exto_preview_is_the_bank_template_filled_from_the_case()
    {
        var draft = await GetAsync(Exto());

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
    public async Task A_case_with_no_fee_shows_a_dash_not_a_zero_fee()
    {
        NoFee();

        var draft = await GetAsync(Exto());

        Assert.Contains("ค่าธรรมเนียม : -<br>", draft.BodyHtml);
    }

    [Fact]
    public async Task A_fee_with_nothing_billable_yet_shows_a_dash()
    {
        _sender.Send(Arg.Is<GetFeePreviewQuery>(q => q.AppraisalId == AppraisalId), Arg.Any<CancellationToken>())
            .Returns(new FeePreviewResult(null, null, new FeeBaseSourceDto(FeeBaseKinds.None, null, null, null, null, null, null),
                null, [new FeePreviewItemDto(Guid.NewGuid(), "01", "Appraisal fee", 2500m, "User")],
                HasBillableBase: false, 0m, 7m, 0m, 0m));

        var draft = await GetAsync(Exto());

        Assert.Contains("ค่าธรรมเนียม : -<br>", draft.BodyHtml);
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

        Assert.StartsWith("<p>เรียน Acme Appraisal</p>", draft.BodyHtml);
    }

    [Fact]
    public async Task Offline_method_is_recognised_whatever_its_casing()
    {
        var draft = await GetAsync(Exto() with { AssignmentMethod = "offline" });

        Assert.Equal(["IntAdminAll", "Keyer", "KeyerSupervisor"], draft.Cc);
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

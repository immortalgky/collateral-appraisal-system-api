using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Application.Providers.Sections;
using Reporting.Application.Services;
using Reporting.Infrastructure.PdfAssembly;
using Reporting.Infrastructure.Rendering;
using Reporting.Tests.Infrastructure;
using Shared.Configurations;

namespace Reporting.Tests;

/// <summary>Code-review round 1 on the appraisal book (items numbered as in the review).</summary>
public class BookReviewRound1Tests
{
    private static Task<string> RenderAsync(string template, AppraisalSummaryModel m) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(template, m, CancellationToken.None);

    /// <summary>How the loaders order a nullable CreatedAt: rows without one come last.</summary>
    internal static string NullsLast(string column) => $"CASE WHEN {column} IS NULL THEN 1 ELSE 0 END, {column}";

    private static DataTable Empty() { var t = new DataTable(); t.Columns.Add("Unused", typeof(int)); return t; }

    // ── 1. the condo owner is each unit's own ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("  นายตัวอย่าง ", true, "นายตัวอย่าง")]
    [InlineData("นายตัวอย่าง", false, "ไม่สามารถตรวจสอบได้")]
    [InlineData(null, null, "-")]
    public void A_unit_prints_its_own_owner_by_the_building_rule(string? name, bool? verified, string expected) =>
        new CondoUnitAreaRow { OwnerName = name, IsOwnerVerified = verified }.OwnerText.Should().Be(expected);

    private static CondoSection TwoOwners() => new()
    {
        OwnerName = "นายเอ", IsOwnerVerified = true,   // the first unit's, as the loader fills them
        Units = [new CondoUnitAreaRow { RoomNumber = "101", OwnerName = "นายเอ", IsOwnerVerified = true }, new CondoUnitAreaRow { RoomNumber = "102", OwnerName = "นายบี", IsOwnerVerified = false }]
    };

    [Fact]
    public async Task The_2_1_room_table_gives_every_room_its_own_owner()
    {
        var html = await RenderAsync("{{ include 'partials/book-flow-condo' }}", new AppraisalSummaryModel { BodyType = "condo", CondoSection = TwoOwners() });

        html.Should().Contain("<td>นายเอ</td>").And.Contain("<td>ไม่สามารถตรวจสอบได้</td>").And.NotContain("นายบี");
    }

    [Fact]
    public async Task The_mixed_book_owner_row_is_one_name_when_the_units_are_alike_and_one_line_per_room_when_they_differ()
    {
        const string tpl = "{{ include 'partials/book-funcs' }}{{ n3 = 0 }}{{ include 'partials/book-mixed-extras' }}";
        var alike = new CondoSection { OwnerName = "นายเอ", Units = [new CondoUnitAreaRow { RoomNumber = "101", OwnerName = "นายเอ" }, new CondoUnitAreaRow { RoomNumber = "102", OwnerName = "นายเอ" }] };

        TwoOwners().OwnersText.Should().Be("ห้อง 101: นายเอ\nห้อง 102: ไม่สามารถตรวจสอบได้");
        alike.OwnersText.Should().Be("นายเอ");
        (await RenderAsync(tpl, new AppraisalSummaryModel { BodyType = "standard", CondoSection = alike })).Should().Contain("<span class=\"l\">ผู้ถือกรรมสิทธิ์</span><span class=\"c\">:</span><span class=\"v\">นายเอ</span>");
        (await RenderAsync(tpl, new AppraisalSummaryModel { BodyType = "standard", CondoSection = TwoOwners() })).Should().MatchRegex("ห้อง 101: นายเอ\\r?\\n\\s*ห้อง 102: ไม่สามารถตรวจสอบได้");   // the row is pre-line, so the include's indent after the newline is not shown;
    }

    [Fact]
    public async Task The_condo_loader_fills_each_units_own_owner_and_verification()
    {
        var prms = new DataTable();
        foreach (var n in new[] { "Group", "Code", "Description" }) prms.Columns.Add(n, typeof(string));
        var detail = new DataTable();
        detail.Columns.Add("Id", typeof(Guid)); detail.Columns.Add("RoomNumber", typeof(string)); detail.Columns.Add("OwnerName", typeof(string)); detail.Columns.Add("IsOwnerVerified", typeof(bool));
        detail.Rows.Add(Guid.NewGuid(), "101", "นายเอ", true);
        detail.Rows.Add(Guid.NewGuid(), "102", "นายบี", false);
        var ds = new DataSet();
        ds.Tables.Add(prms); ds.Tables.Add(detail); ds.Tables.Add(Empty()); ds.Tables.Add(Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);

        var condo = await CondoSectionLoader.LoadAsync(f.GetOpenConnection(), Guid.NewGuid());

        condo!.Units.Select(u => u.OwnerText).Should().Equal("นายเอ", "ไม่สามารถตรวจสอบได้");
    }

    // ── 2. a lease is printed once ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_group_with_both_a_leasehold_and_a_profit_rent_page_prints_its_leases_once()
    {
        LeaseAgreementBlock L(string no) => new() { ContractNo = no, LessorName = "ผู้ให้เช่าตัวอย่าง" };
        var m = new AppraisalSummaryModel
        {
            BodyType = "standard",
            LeaseholdSections = [new LeaseholdSection { GroupNumber = 1, Leases = [L("CT-1")] }],
            ProfitRentSections = [new ProfitRentSection { GroupNumber = 1, Leases = [L("CT-1")] }, new ProfitRentSection { GroupNumber = 1, Leases = [L("CT-1")] }, new ProfitRentSection { GroupNumber = 2, Leases = [L("CT-2")] }]
        };

        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ n3 = 0 }}{{ include 'partials/book-mixed-extras' }}", m);

        html.Split("<span class=\"v\">CT-1</span>").Length.Should().Be(2);   // once
        html.Split("<span class=\"v\">CT-2</span>").Length.Should().Be(2);
        html.Should().Contain("สัญญาเช่าที่ 1").And.Contain("สัญญาเช่าที่ 2").And.NotContain("สัญญาเช่าที่ 3");
    }

    // ── 3. ส่วนพัฒนา follows the 3.2 rule on the ext summary page too ────────────────────────────────────────────────

    [Fact]
    public void The_ext_summary_building_block_says_ส่วนพัฒนา_for_a_fence_row_of_a_real_building()
    {
        var m = new AppraisalSummaryModel
        {
            BodyType = "standard",
            PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = [new LandSection()], Buildings = [new BuildingSection { BuildingNumber = 1, DevelopmentItems = [new DevelopmentItem { AreaDescription = "รั้ว" }] }] }]
        };

        ExternalSummaryRows.PropertyBlocks(m).Single(b => b.Title == "สิ่งปลูกสร้าง").Lines[0].Should().StartWith("จำนวน 1 รายการ และส่วนพัฒนาอื่นๆ");
    }

    // ── 4. the condo rooms' obligations reach the combined rows even with a land ────────────────────────────────────

    [Fact]
    public async Task The_builder_hands_over_one_obligation_item_per_condo_room()
    {
        var header = new DataTable();
        header.Columns.Add("AppraisalNumber", typeof(string)); header.Columns.Add("RequestId", typeof(Guid)); header.Columns.Add("CompletedAt", typeof(DateTime));
        header.Rows.Add("A-1", Guid.NewGuid(), DBNull.Value);
        var land = new DataTable(); land.Columns.Add("LandDetailId", typeof(Guid)); land.Rows.Add(Guid.NewGuid());   // a land is present too
        var condo = new DataTable();
        foreach (var c in new[] { "CondoRoomNumber", "CondoObligationType", "CondoMortgageDetails" }) condo.Columns.Add(c, typeof(string));
        condo.Rows.Add("101", "[\"02\"]", "จำนองกับธนาคาร ข");
        var ds = new DataSet();
        for (var i = 0; i < 14; i++) ds.Tables.Add(i == 0 ? header : i == 6 ? land : i == 8 ? condo : Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);

        var m = await ExternalBookBuilder.BuildAsync(f.GetOpenConnection(), Guid.NewGuid(), new AppraisalSummaryModel(), 70m, CancellationToken.None);

        m.CondoObligationItems.Should().ContainSingle().Which.Should().BeEquivalentTo(new ExternalObligationItem { Label = "ห้อง 101", Mortgage = "จำนองกับธนาคาร ข", Others = "ไม่มี" });
    }

    // ── 7. a captioned attachment keeps its page's /Rotate and /CropBox ─────────────────────────────────────────────

    private sealed class BlankRenderer : IPdfRenderer
    {
        public Task<byte[]> RenderAsync(string html, CancellationToken ct)
        {
            using var d = new PdfDocument();
            for (var i = 0; i < Math.Max(1, System.Text.RegularExpressions.Regex.Matches(html, "class=\"ov").Count); i++) d.AddPage();
            using var ms = new MemoryStream();
            d.Save(ms);
            return Task.FromResult(ms.ToArray());
        }
    }

    private sealed class FixedFile(string path) : IReportAttachmentSource
    {
        public Task<string?> GetFilePathAsync(Guid documentId, CancellationToken cancellationToken) => Task.FromResult<string?>(path);
    }

    private static async Task<List<(double W, double H, int Rotate)>> CaptionedPagesAsync(Action<PdfDocument> build)
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var src = new PdfDocument()) { build(src); src.Save(path); }
            var segments = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
                .RenderAsync("<html><body><!-- PART: body s1 -->x<!-- SLOT: a --><!-- CAPTION: แผนที่ --><!-- pdf-margin-y:24mm --></body></html>", new { }, CancellationToken.None);
            var withIds = segments.Select(s => s is AttachmentSlotSegment slot ? slot with { DocumentIds = [Guid.NewGuid()] } : s).ToList();
            var pdf = await new PdfSharpAssembler(new BlankRenderer(), new FixedFile(path), Options.Create(new FileStorageConfiguration()), NullLogger<PdfSharpAssembler>.Instance)
                .AssembleAsync(withIds, CancellationToken.None);
            using var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            return doc.Pages.Cast<PdfPage>().Skip(1).Select(p => (p.Width.Point, p.Height.Point, p.Rotate)).ToList();   // page 1 is the body part
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task A_rotated_attachment_page_keeps_its_rotation_and_a_cropped_one_comes_out_at_its_crop_size()
    {
        var pages = await CaptionedPagesAsync(src =>
        {
            var plain = src.AddPage(); plain.Width = XUnit.FromPoint(595); plain.Height = XUnit.FromPoint(842);
            var rotated = src.AddPage(); rotated.Width = XUnit.FromPoint(595); rotated.Height = XUnit.FromPoint(842); rotated.Rotate = 90;
            var cropped = src.AddPage(); cropped.Width = XUnit.FromPoint(595); cropped.Height = XUnit.FromPoint(842); cropped.CropBox = new PdfRectangle(new XRect(50, 50, 300, 400));
        });

        pages.Should().HaveCount(3);
        (pages[0].W, pages[0].H).Should().Be((595, 842));
        pages[1].Rotate.Should().Be(90);                     // the rotated page keeps its /Rotate, so a viewer shows it landscape
        (pages[2].W, pages[2].H).Should().Be((300, 400));   // only the crop box
    }

    // ── 8. the condo 3.2 plan ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_condo_room_plan_under_3_2_is_printed()
    {
        var m = new AppraisalSummaryModel
        {
            BodyType = "condo", CondoSection = new CondoSection { Units = [new CondoUnitAreaRow { RoomNumber = "101" }] },
            AppendixSection = new AppendixSection { Groups = [new AppendixGroup { TypeCode = "BLUEPRINT", Images = [new AppendixImage { ImgSrc = "file:///plan.png", Caption = "ผังห้อง" }] }] }
        };

        var html = await RenderAsync("{{ include 'partials/book-flow-condo' }}", m);

        var heading = html.IndexOf("ห้องชุดที่ประเมิน", StringComparison.Ordinal);
        var plan = html.IndexOf("<img alt=\"\" src=\"file:///plan.png\"/>", StringComparison.Ordinal);
        plan.Should().BeGreaterThan(heading).And.BeLessThan(html.IndexOf("<table class=\"vt\">", heading, StringComparison.Ordinal));
        html.Should().Contain("<div class=\"cap\">ผังห้อง</div>");
    }

    // ── 9. the "ผู้ประเมินกำหนด" land value flag ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(5_330_123, 5_330_123, false)]   // pricing's own whole-baht product (a Cost method on a per-wa rate)
    [InlineData(5_330_000, 5_330_123, false)]   // the indicated value a Role = Land method copies, rounded to 1,000
    [InlineData(5_400_000, 5_330_123, true)]    // typed over
    [InlineData(5_330_124, 5_330_123, true)]
    public void The_land_value_is_overridden_only_when_it_is_neither_the_product_nor_its_rounded_thousand(int stored, int product, bool expected) =>
        AppraisalSummaryLandBuildingDataProvider.IsLandValueOverridden(stored, product).Should().Be(expected);

    // ── 10 / 11. SQL that has to hold ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_approach_queries_order_by_when_the_approaches_were_created_then_type_then_id()
    {
        var approaches = new DataTable();
        foreach (var c in new[] { "GroupNumber", "ApproachId", "ApproachType" }) approaches.Columns.Add(c, c == "ApproachId" ? typeof(Guid) : c == "GroupNumber" ? typeof(int) : typeof(string));
        var f = new FakeSqlConnectionFactory();
        f.EnqueueResult(approaches);
        await ExternalBookBuilder.LoadPricingAsync(f.GetOpenConnection(), Guid.NewGuid(), CancellationToken.None);

        var picker = new FakeSqlConnectionFactory();
        picker.EnqueueResult(Empty());
        await PricingMethodPicker.LoadAsync(picker.GetOpenConnection(), Guid.NewGuid(), ["WQS"], CancellationToken.None);

        f.Commands.Should().ContainSingle().Which.Should().Contain($"ORDER BY pg.GroupNumber, paa.IsSelected DESC, {NullsLast("paa.CreatedAt")}, paa.ApproachType, paa.Id");
        picker.Commands.Should().ContainSingle().Which.Should().Contain($"paa.IsSelected DESC, {NullsLast("paa.CreatedAt")}, paa.ApproachType, paa.Id, {NullsLast("pam.CreatedAt")}");
    }

    // ── 13. the block loader's text helpers ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_block_loader_reads_a_dash_only_name_as_none_and_the_launch_date_in_thai()
    {
        var project = new DataTable();
        project.Columns.Add("Id", typeof(Guid)); project.Columns.Add("ProjectType", typeof(string)); project.Columns.Add("ProjectName", typeof(string)); project.Columns.Add("Developer", typeof(string)); project.Columns.Add("ProjectSaleLaunchDate", typeof(string));
        project.Rows.Add(Guid.NewGuid(), "L", " - ", "บริษัทตัวอย่าง", "2026-03-05");
        var ds = new DataSet();
        for (var i = 0; i < 15; i++) ds.Tables.Add(i == 1 ? project : Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);

        var block = await BlockBookSectionLoader.LoadAsync(f.GetOpenConnection(), Guid.NewGuid(), d => d);

        block!.Project.ProjectName.Should().BeNull();
        block.Project.Developer.Should().Be("บริษัทตัวอย่าง");
        block.Project.SaleLaunchDate.Should().Be("5 มีนาคม 2569");
    }
}

using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Application.Services;
using Reporting.Infrastructure.PdfAssembly;
using Reporting.Infrastructure.Rendering;
using Shared.Configurations;

namespace Reporting.Tests;

/// <summary>The external book's สรุปผลการประเมินมูลค่าทรัพย์สิน page (a numbered front part after the contents) and the internal summary part's single header.</summary>
public class BookExtSummaryPageTests
{
    private static Task<string> PageAsync(AppraisalSummaryModel m)
    {
        m.PropertyBlocks = ExternalSummaryRows.PropertyBlocks(m);
        return new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/book-ext-summary' }}", m, CancellationToken.None);
    }

    private static LandSection Land(string number, string owner = "นายเอ", string? eviction = null, string? remark = null, decimal wa = 400m) => new()
    {
        TotalAreaInWa = wa, EvictionType = eviction, EvictionRemark = remark,
        Titles = [new LandTitleRow { TitleNumber = number, TitleTypeDisplay = "โฉนดที่ดิน", OwnerName = owner }]
    };

    private static AppraisalSummaryModel Full(bool insurance = true) => new()
    {
        IsExternal = true, BodyType = "standard", CustomerName = "นายลูกค้า ตัวอย่าง", AppraisalDate = new DateTime(2026, 10, 28), AppraisalPurpose = "เพื่อพิจารณาสินเชื่อ",
        AppraisalCriteria = "เพื่อทราบมูลค่าตลาด", PropertyTypeSummary = "ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 1 รายการ บนที่ดิน", LocationText = "ตั้งอยู่เลขที่ 1 ตำบลตัวอย่าง",
        TitleDeedGroups = "โฉนดที่ดินเลขที่ 12345", TotalTitleDeeds = 1, LandAreaWords = "1 ไร่ 0 งาน 0 ตารางวา (หรือ 400 ตารางวา)", LandOwner = "นายเอ", BuildingOwner = "นายเอ", CondoOwner = "นายบี",
        ObligationMortgage = "ไม่มี", ObligationOthers = "ไม่มี", EvictionText = "ไม่มี", CityPlan = "พื้นที่สีเหลือง", ApproachMethodsText = "วิธีต้นทุน (Cost Approach)",
        CollateralValue = 12_450_000m, ForcedSaleValue = 8_715_000m, ForceSaleRate = 70m, BuildingCoverageAmount = insurance ? 3_200_000m : null,
        PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = [Land("12345")], Buildings = [new BuildingSection { BuildingNumber = 1 }] }]
    };

    [Fact]
    public async Task The_rows_come_in_the_banks_order_under_a_centred_title()
    {
        var html = await PageAsync(Full());

        string[] order =
        [
            "สรุปผลการประเมินมูลค่าทรัพย์สิน", "ชื่อลูกค้า", "วันที่ประเมินมูลค่า", "วัตถุประสงค์", "หลักเกณฑ์การประเมินมูลค่า", "ประเภททรัพย์สิน", "ที่ตั้งทรัพย์สิน",
            "รายละเอียดทรัพย์สิน", "ผู้ถือกรรมสิทธิ์ที่ดิน", "ผู้ถือกรรมสิทธิ์สิ่งปลูกสร้าง", "ผู้ถือกรรมสิทธิ์ห้องชุด", "ภาระผูกพันการจำนอง", "ภาระผูกพันอื่นๆ", "การรอนสิทธิ",
            "ข้อจำกัดทางผังเมือง", "วิธีการประเมินมูลค่า", "มูลค่าตลาดตามสภาพปัจจุบัน"
        ];
        var at = order.Select(o => html.IndexOf(o, StringComparison.Ordinal)).ToList();
        at.Should().OnlyContain(i => i >= 0).And.BeInAscendingOrder();
        html.Should().Contain("<div class=\"bk-title\">สรุปผลการประเมินมูลค่าทรัพย์สิน</div>").And.Contain("<span class=\"c\">:</span>")
            .And.Contain("บริษัทฯ เลือกใช้วิธีต้นทุน (Cost Approach) เป็นเกณฑ์ในการกำหนดมูลค่าตลาดของทรัพย์สิน").And.NotContain("พิกัด");
    }

    [Fact]
    public async Task A_row_with_no_value_is_left_out_but_the_city_plan_row_stays_with_a_red_dash()
    {
        var html = await PageAsync(new AppraisalSummaryModel { IsExternal = true, CustomerName = "นายลูกค้า ตัวอย่าง" });

        html.Should().Contain("ชื่อลูกค้า").And.Contain("<span class=\"l\">ข้อจำกัดทางผังเมือง</span><span class=\"c\">:</span><span class=\"v\"><span class=\"na\">-</span></span>")
            .And.NotContain("วันที่ประเมินมูลค่า").And.NotContain("วัตถุประสงค์").And.NotContain("หลักเกณฑ์").And.NotContain("ที่ตั้งทรัพย์สิน").And.NotContain("รายละเอียดทรัพย์สิน")
            .And.NotContain("ผู้ถือกรรมสิทธิ์").And.NotContain("ภาระผูกพัน").And.NotContain("การรอนสิทธิ").And.NotContain("วิธีการประเมินมูลค่า").And.NotContain("bk-valbox");
    }

    // ── รายละเอียดทรัพย์สิน ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Land_with_buildings_reads_the_opening_then_a_land_block_and_a_building_block_with_the_section_pointer()
    {
        var m = Full();
        m.BuildingDetailsText = "บ้านเดี่ยว 3 ชั้น แบบ A1 จำนวน 2 หลัง\nโรงจอดรถ 1 ชั้น จำนวน 1 หลัง";
        m.PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = [Land("12345")], Buildings = [new BuildingSection { BuildingNumber = 1 }, new BuildingSection { BuildingNumber = 2 }, new BuildingSection { IsDevelopmentOnly = true, DevelopmentItems = [new DevelopmentItem()] }] }];

        var html = await PageAsync(m);

        html.Should().Contain("<div>ทรัพย์สินที่ประเมินมูลค่า เป็นที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 1 รายการ บนที่ดิน ประกอบด้วย</div>")
            .And.Contain("<div class=\"pb\"><b>ที่ดิน</b> ประกอบด้วยเอกสารสิทธิ์ จำนวน 1 ฉบับ ได้แก่ โฉนดที่ดินเลขที่ 12345</div>")
            .And.Contain("<div class=\"pl\">เนื้อที่ดินรวมตามเอกสารสิทธิ์ 1 ไร่ 0 งาน 0 ตารางวา <span style=\"white-space:nowrap\">(หรือ 400 ตารางวา)</span></div>")
            .And.Contain("<div class=\"pb\"><b>สิ่งปลูกสร้าง</b> จำนวน 2 รายการ และส่วนพัฒนาอื่นๆ (ดูรายละเอียดตามหัวข้อ 3.2)</div>")
            .And.Contain("<div class=\"pl\">บ้านเดี่ยว 3 ชั้น แบบ A1 จำนวน 2 หลัง</div>").And.Contain("<div class=\"pl\">โรงจอดรถ 1 ชั้น จำนวน 1 หลัง</div>")
            .And.NotContain("ห้องชุด</b>").And.NotContain("เครื่องจักร</b>");
    }

    [Fact]
    public async Task Above_ten_deeds_the_land_block_carries_the_per_type_summary_and_its_pointer()
    {
        var m = new AppraisalSummaryModel { IsExternal = true, BodyType = "standard", TitleDeedGroups = "โฉนดที่ดิน จำนวน 48 ฉบับ และ น.ส.3 ก. จำนวน 2 ฉบับ", TotalTitleDeeds = 50, TitleDeedsSummarised = true, UnnumberedTitleDeeds = 1 };

        var html = await PageAsync(m);

        html.Should().Contain("<b>ที่ดิน</b> ประกอบด้วยเอกสารสิทธิ์ จำนวน 50 ฉบับ ได้แก่ โฉนดที่ดิน จำนวน 48 ฉบับ และ น.ส.3 ก. จำนวน 2 ฉบับ (ไม่ระบุเลขที่ 1 ฉบับ) (รายละเอียดตามข้อ 2.1)");
    }

    [Fact]
    public async Task The_area_parenthetical_is_kept_in_one_piece_so_a_line_never_breaks_inside_ตารางวา()
    {
        var m = new AppraisalSummaryModel { IsExternal = true, BodyType = "standard", TitleDeedGroups = "โฉนดที่ดินเลขที่ 1", TotalTitleDeeds = 1, LandAreaWords = "12 ไร่ 2 งาน 0 ตารางวา (หรือ 5,000 ตารางวา)" };

        var html = await PageAsync(m);

        html.Should().Contain("เนื้อที่ดินรวมตามเอกสารสิทธิ์ 12 ไร่ 2 งาน 0 ตารางวา <span style=\"white-space:nowrap\">(หรือ 5,000 ตารางวา)</span>");
    }

    [Fact]
    public async Task A_condo_has_a_condo_block_and_no_land_or_building_block()
    {
        var m = new AppraisalSummaryModel
        {
            IsExternal = true, BodyType = "condo", PropertyTypeSummary = "ห้องชุด จำนวน 2 ห้อง",
            CondoSection = new CondoSection { Units = [new CondoUnitAreaRow(), new CondoUnitAreaRow()], UsableArea = 71.5m }
        };

        var html = await PageAsync(m);

        html.Should().Contain("<div>ทรัพย์สินที่ประเมินมูลค่า เป็นห้องชุด จำนวน 2 ห้อง ประกอบด้วย</div>").And.Contain("<div class=\"pb\"><b>ห้องชุด</b> จำนวน 2 ห้อง เนื้อที่ห้องชุดรวม 71.5 ตารางเมตร</div>")
            .And.NotContain("<b>ที่ดิน</b>").And.NotContain("<b>สิ่งปลูกสร้าง</b>");
    }

    [Fact]
    public async Task Machinery_has_a_machine_block_with_the_count_and_the_registrations()
    {
        var m = new AppraisalSummaryModel
        {
            IsExternal = true, BodyType = "machine", PropertyTypeSummary = "เครื่องจักรและอุปกรณ์ จำนวน 3 รายการ",
            MachineSection = new MachineSection { Machines = [new MachineRow(), new MachineRow(), new MachineRow()] },
            MachineRegistrations = ["51-414-701-0001 ถึง 0029", "AB-77"]
        };

        var html = await PageAsync(m);

        html.Should().Contain("<b>เครื่องจักร</b> จำนวน 3 รายการ").And.Contain("<div class=\"pl\">เลขทะเบียน</div>").And.Contain("<div class=\"pl\">(1) 51-414-701-0001 ถึง 0029</div>").And.Contain("<div class=\"pl\">(2) AB-77</div>");
    }

    [Fact]
    public async Task A_book_with_none_of_those_parts_has_no_property_detail_row()
    {
        var html = await PageAsync(new AppraisalSummaryModel { IsExternal = true, BodyType = "block", PropertyTypeSummary = "ที่ดินพร้อมสิ่งปลูกสร้าง จำนวน 5 หน่วย" });

        html.Should().NotContain("รายละเอียดทรัพย์สิน");
    }

    // ── การรอนสิทธิ ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static AppraisalSummaryModel Lands(params LandSection[] lands) => new() { BodyType = "standard", PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = lands }] };

    [Fact]
    public void Eviction_reads_the_stored_text_with_its_remark_and_ไม่มี_when_none_is_recorded()
    {
        LetterOwnerRows.LandEvictions(Lands(Land("1", eviction: "ทางภาระจำยอม", remark: "ผ่านด้านทิศเหนือ"))).Should().Be("ทางภาระจำยอม");   // the typed remark is not on this page (it stays in 2.2)
        LetterOwnerRows.LandEvictions(Lands(Land("1"))).Should().Be("ไม่มี");
        LetterOwnerRows.LandEvictions(Lands(Land("1", eviction: " - "), Land("2"))).Should().Be("ไม่มี");   // every deed none
        LetterOwnerRows.LandEvictions(new AppraisalSummaryModel()).Should().BeNull();
    }

    [Fact]
    public async Task A_typed_eviction_remark_never_reaches_the_summary_page_row()
    {
        var lands = Lands(Land("1", eviction: "ทางภาระจำยอม", remark: "ผ่านด้านทิศเหนือ"), Land("2", eviction: "สิทธิเหนือพื้นดิน", remark: "หมายเหตุตัวอย่าง"));

        var text = LetterOwnerRows.LandEvictions(lands);
        var html = await PageAsync(new AppraisalSummaryModel { IsExternal = true, BodyType = "standard", EvictionText = text });

        text.Should().Be("โฉนดที่ดิน 1: ทางภาระจำยอม\nโฉนดที่ดิน 2: สิทธิเหนือพื้นดิน").And.NotContain("หมายเหตุ").And.NotContain("ผ่านด้านทิศเหนือ");
        html.Should().Contain("<span class=\"l\">การรอนสิทธิ</span>").And.NotContain("หมายเหตุ").And.NotContain("ผ่านด้านทิศเหนือ");
    }

    [Fact]
    public void Differing_evictions_are_paired_with_their_deed_and_above_ten_point_at_section_2_2()
    {
        // only the deeds that have an entry are listed
        LetterOwnerRows.LandEvictions(Lands(Land("1", eviction: "ทางภาระจำยอม"), Land("2"), Land("3", eviction: "สิทธิเหนือพื้นดิน"))).Should().Be("โฉนดที่ดิน 1: ทางภาระจำยอม\nโฉนดที่ดิน 3: สิทธิเหนือพื้นดิน");
        LetterOwnerRows.LandEvictions(Lands(Land("1", eviction: "ทางภาระจำยอม"), Land("2"))).Should().Be("โฉนดที่ดิน 1: ทางภาระจำยอม");   // not a bare text that would read as applying to every deed

        // 12 deeds: 6 with one entry, 5 with another, 1 with none; the one with none is not counted
        var many = Enumerable.Range(1, 12).Select(i => Land(i.ToString(), eviction: i <= 6 ? "ทางภาระจำยอม" : i <= 11 ? "สิทธิเหนือพื้นดิน" : null)).ToArray();
        LetterOwnerRows.LandEvictions(Lands(many)).Should().Be("ทางภาระจำยอม จำนวน 6 ฉบับ\nสิทธิเหนือพื้นดิน จำนวน 5 ฉบับ (รายละเอียดตามข้อ 2.2)");

        // 5 of 50 deeds: the 5 are listed (at most 10 entries), the other 45 are not mentioned
        var fiveOfFifty = Enumerable.Range(1, 50).Select(i => Land(i.ToString(), eviction: i <= 5 ? "ทางภาระจำยอม" : null)).ToArray();
        LetterOwnerRows.LandEvictions(Lands(fiveOfFifty)).Should().Be(string.Join("\n", Enumerable.Range(1, 5).Select(i => $"โฉนดที่ดิน {i}: ทางภาระจำยอม")));
    }

    // ── วิธีการประเมิน, value box, location ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_approach_sentence_names_each_used_approach_once_and_skips_reference_ones()
    {
        var rows = new List<ApproachValueRow>
        {
            new() { ApproachType = "Cost" }, new() { ApproachType = "Cost" }, new() { ApproachType = "Market" }, new() { ApproachType = "Income", IsReference = true }
        };

        PricingNarrative.SelectedApproaches(rows).Should().Be("วิธีต้นทุน (Cost Approach) และวิธีเปรียบเทียบตลาด (Market Approach)");
        PricingNarrative.SelectedApproaches([new ApproachValueRow { ApproachType = "Income", IsReference = true }]).Should().BeNull();
    }

    [Fact]
    public async Task The_value_box_has_the_header_and_the_market_forced_sale_and_insurance_rows()
    {
        var html = await PageAsync(Full());

        html.Should().Contain("<div class=\"t\">มูลค่าตลาดตามสภาพปัจจุบัน</div>")
            .And.Contain("&bull; มูลค่าตลาด</div><div>(Market Value)</div>").And.Contain("<div>12,450,000.- บาท</div><div class=\"b\">(สิบสองล้านสี่แสนห้าหมื่นบาทถ้วน)</div>")
            .And.Contain("&bull; มูลค่าบังคับขาย (70%)</div><div>(Forced Sale Value)</div>").And.Contain("<div>8,715,000.- บาท</div>")
            .And.Contain("&bull; ทุนประกันภัยสิ่งปลูกสร้าง</div><div>(Insurance Value)</div>").And.Contain("<div>3,200,000.- บาท</div>");

        (await PageAsync(Full(insurance: false))).Should().NotContain("Insurance Value");
    }

    [Fact]
    public void The_location_is_the_covers_address_line()
    {
        CoverNarrative.LocationLine(new AppraisalSummaryModel { IsExternal = true, CollateralLocation = "เลขที่ 12/3 ตำบลตัวอย่าง" }).Should().Be("ตั้งอยู่เลขที่ 12/3 ตำบลตัวอย่าง");
        CoverNarrative.LocationLine(new AppraisalSummaryModel { IsExternal = true, CollateralLocation = "หมู่ที่ 4" }).Should().Be("ตั้งอยู่ หมู่ที่ 4");
        CoverNarrative.LocationLine(new AppraisalSummaryModel { IsExternal = true, CollateralLocation = "-" }).Should().BeNull();
    }

    // ── part order, the contents line, the numbering and the header ──────────────────────────────────────────────────

    private static Task<IReadOnlyList<RenderSegment>> SegmentsAsync(AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderAsync("{{ include 'appraisal-book' }}", model, CancellationToken.None);

    [Fact]
    public async Task An_external_book_has_the_summary_page_as_a_front_part_after_the_contents_and_lists_it_first()
    {
        var segments = await SegmentsAsync(new AppraisalSummaryModel { IsExternal = true, BodyType = "standard", AppraisalBookNumber = "A-1" });

        var parts = segments.OfType<HtmlSegment>().Select(s => (s.Part!.Kind, s.Part.Id)).ToList();
        var toc = parts.IndexOf((BookPartKind.Front, "toc"));
        parts.IndexOf((BookPartKind.Front, "extsummary")).Should().Be(toc + 1);
        parts.Should().NotContain(p => p.Id == "summary");   // the internal summary form is not in the external book
        var tocHtml = segments.OfType<HtmlSegment>().First(s => s.Part is { Id: "toc" }).Html;
        tocHtml.Should().Contain("<span>สรุปผลการประเมินมูลค่าทรัพย์สิน</span><i></i><span>[[PAGE:extsummary]]</span>");
        Regex.Match(tocHtml, @"\[\[PAGE:(?<id>\w+)\]\]").Groups["id"].Value.Should().Be("extsummary");   // the first line of the list
    }

    [Fact]
    public async Task The_letter_part_is_raw_with_its_own_ruled_table_and_the_summary_page_is_a_separate_front_part_with_its_own_rows()
    {
        var segments = await SegmentsAsync(new AppraisalSummaryModel { IsExternal = true, BodyType = "standard", CustomerName = "นายลูกค้า ตัวอย่าง", CollateralValue = 1m });

        var first = segments.OfType<HtmlSegment>().First();
        first.Part!.Kind.Should().Be(BookPartKind.Raw);
        first.Html.Should().Contain("<table class=\"kv-table\">").And.Contain("<th class=\"kv-label\" scope=\"row\">ลูกค้า</th>").And.NotContain("<div class=\"bk-valbox\">").And.NotContain("class=\"vk bk-ext\"");   // markup, not the shared stylesheet
        var page = segments.OfType<HtmlSegment>().Single(s => s.Part is { Kind: BookPartKind.Front, Id: "extsummary" });
        page.Html.Should().Contain("<span class=\"l\">ชื่อลูกค้า</span>").And.Contain("<div class=\"bk-valbox\">").And.NotContain("<table class=\"kv-table\">");
    }

    private sealed class PagesRenderer(string summaryHtml, int summaryPages = 2) : IPdfRenderer
    {
        public List<string> Htmls { get; } = [];

        public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken)
        {
            lock (Htmls) Htmls.Add(html);
            var pages = Regex.Matches(html, "class=\"ov").Count is var ov and > 0 ? ov : html == summaryHtml ? summaryPages : 1;
            using var doc = new PdfDocument();
            for (var i = 0; i < pages; i++) doc.AddPage();
            using var ms = new MemoryStream();
            doc.Save(ms);
            return Task.FromResult(ms.ToArray());
        }
    }

    private static PdfSharpAssembler Assembler(PagesRenderer renderer) =>
        new(renderer, null!, Options.Create(new FileStorageConfiguration()), NullLogger<PdfSharpAssembler>.Instance);

    [Fact]
    public async Task The_external_summary_page_is_numbered_II_and_keeps_the_running_header()
    {
        var segments = await SegmentsAsync(new AppraisalSummaryModel { IsExternal = true, BodyType = "standard", AppraisalBookNumber = "A-1" });
        var renderer = new PagesRenderer(segments.OfType<HtmlSegment>().First(x => x.Part is { Id: "extsummary" }).Html, 1);

        await Assembler(renderer).AssembleAsync(segments.ToList(), CancellationToken.None);

        renderer.Htmls.Last(h => h.Contains("สารบัญ")).Should().Contain("<span>สรุปผลการประเมินมูลค่าทรัพย์สิน</span><i></i><span>II</span>");
        var overlay = renderer.Htmls.Single(h => h.Contains("class=\"ov"));
        overlay.Should().Contain("- II -");
        Regex.Matches(overlay, "class=\"hd\"").Count.Should().Be(Regex.Matches(overlay, "class=\"ft\"").Count);   // every stamped page has its header
    }

    [Fact]
    public async Task The_internal_summary_pages_get_the_footer_and_page_number_but_no_overlay_header()
    {
        var segments = await SegmentsAsync(new AppraisalSummaryModel { BodyType = "standard", AppraisalBookNumber = "A-1" });
        var renderer = new PagesRenderer(segments.OfType<HtmlSegment>().First(x => x.Part is { Id: "summary" }).Html, 2);

        await Assembler(renderer).AssembleAsync(segments.ToList(), CancellationToken.None);

        var overlay = renderer.Htmls.Single(h => h.Contains("class=\"ov"));
        var footers = Regex.Matches(overlay, "class=\"ft\"").Count;
        overlay.Should().Contain("- II -").And.Contain("- III -");
        Regex.Matches(overlay, "class=\"hd\"").Count.Should().Be(footers - 2);   // the 2 summary pages have a footer and no header; every other numbered page has both
    }

    [Fact]
    public void An_overlay_page_can_leave_out_its_header_and_keeps_the_footer()
    {
        var html = BookChrome.OverlayHtml([new BookChrome.OverlayPage("II", false, null, NoHeader: true), new BookChrome.OverlayPage("1", false)], "A-1");

        Regex.Matches(html, "<div class=\"hd\">").Count.Should().Be(1);
        Regex.Matches(html, "<div class=\"ft\">").Count.Should().Be(2);
        html.Should().Contain("- II -").And.Contain("- 1 -");
    }
}

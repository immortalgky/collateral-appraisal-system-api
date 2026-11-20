using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Application.Providers.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Book section 1.5 (flood / pollution) tick rules, the "-" unit rule of vk, and code 99 printing the *Other text.</summary>
public class BookPropertyProblemsTests
{
    // Section 1.5: only the selected problems print, each on its own row; "ไม่มี" when answered with none, "-" when never answered.
    private static string Row(string label, string value) =>
        $"<span class=\"l\">{label}</span><span class=\"c\">:</span><span class=\"v\">{value}";

    private const string NoneRow = "<span class=\"l\">ปัญหาสภาพแวดล้อม</span><span class=\"c\">:</span><span class=\"v\">ไม่มี</span>";
    private const string DashRow = "<span class=\"l\">ปัญหาสภาพแวดล้อม</span><span class=\"c\">:</span><span class=\"v\">-</span>";
    private const string FloodLabel = "<span class=\"l\">ปัญหาเรื่องน้ำท่วมขัง</span>";
    private const string PollutionLabel = "<span class=\"l\">ปัญหาด้านมลพิษ</span>";
    private const string OtherLabel = "<span class=\"l\">ปัญหาอื่นๆ</span>";

    private static Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync(template, model, CancellationToken.None);

    private static Task<string> RenderLandAsync(LandSection land) =>
        RenderAsync("{{ include 'partials/book-funcs' }}{{ multi_land = false }}{{ include 'partials/book-land-location' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = [land] }] });

    [Fact]
    public async Task The_book_prints_no_electricity_distance_row_even_when_the_data_has_one()
    {
        var html = await RenderLandAsync(new LandSection { ElectricityDistance = 120m, FarFromElectricity = true });

        html.Should().NotContain("ไฟฟ้า").And.Contain("<span class=\"l\">สาธารณูปโภค</span>");
    }

    private static Task<string> RenderCondoAsync(CondoSection condo) =>
        RenderAsync("{{ include 'partials/book-flow-condo' }}", new AppraisalSummaryModel { CondoSection = condo });

    [Fact]
    public async Task The_condo_prints_other_legal_limitations_as_a_sub_headed_paragraph_after_the_limitation_rows()
    {
        var html = await RenderCondoAsync(new CondoSection { OtherLegalLimitations = "ภาระจำยอมทางเดินร่วม\nลานจอดรถส่วนกลาง" });

        html.Should().Contain("<div class=\"vk-sub\">ข้อจำกัดอื่นๆ ที่พบ</div>")
            .And.Contain("white-space:pre-line;overflow-wrap:anywhere;line-height:1.75;font-size:10.5pt;padding-left:14px;\">ภาระจำยอมทางเดินร่วม\nลานจอดรถส่วนกลาง</div>");
        html.IndexOf("ข้อจำกัดอื่นๆ ที่พบ", StringComparison.Ordinal).Should().BeGreaterThan(html.IndexOf("แนวเขตป่าไม้", StringComparison.Ordinal));
        html.Should().NotContain("การรอนสิทธิอื่นๆ"); // the new paragraph replaces the old blank write-in row
        html.IndexOf("ข้อจำกัดอื่นๆ ที่พบ", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("ราคาประเมินราชการห้องชุด", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task The_condo_prints_no_other_legal_limitations_heading_when_blank(string? text)
    {
        var html = await RenderCondoAsync(new CondoSection { OtherLegalLimitations = text });

        html.Should().NotContain("ข้อจำกัดอื่นๆ ที่พบ");
    }

    public static TheoryData<string> Bodies => ["land", "condo"];

    private static Task<string> RenderBothAsync(string body, bool? flood = null, bool? pollution = null, bool? other = null,
        string? floodRemark = null, string? pollutionType = null, string? pollutionRemark = null, string? otherRemark = null) =>
        body == "land"
            ? RenderLandAsync(new LandSection { HasFloodProblem = flood, HasPollutionProblem = pollution, HasOtherEnvironmentProblem = other,
                FloodProblemRemark = floodRemark, PollutionType = pollutionType, PollutionProblemRemark = pollutionRemark, OtherEnvironmentProblemRemark = otherRemark })
            : RenderCondoAsync(new CondoSection { HasFloodProblem = flood, HasPollutionProblem = pollution, HasOtherEnvironmentProblem = other,
                FloodProblemRemark = floodRemark, PollutionType = pollutionType, PollutionProblemRemark = pollutionRemark, OtherEnvironmentProblemRemark = otherRemark });

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task Flood_alone_prints_its_row_with_the_remark_on_its_own_line(string body)
    {
        var html = await RenderBothAsync(body, flood: true, pollution: false, other: false, floodRemark: "ท่วมช่วงฝน");

        html.Should().Contain(Row("ปัญหาเรื่องน้ำท่วมขัง", "มีปัญหาเรื่องน้ำท่วมขัง<span class=\"rm\">หมายเหตุ : ท่วมช่วงฝน</span></span>"))
            .And.NotContain(PollutionLabel).And.NotContain(OtherLabel).And.NotContain(NoneRow).And.NotContain("(ท่วมช่วงฝน)");
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task Pollution_alone_prints_the_type_and_its_remark(string body)
    {
        var html = await RenderBothAsync(body, flood: false, pollution: true, pollutionType: "กลิ่น / เสียง", pollutionRemark: "โรงงานข้างเคียง");

        html.Should().Contain(Row("ปัญหาด้านมลพิษ", "ตั้งอยู่ใกล้กับสถานที่ก่อปัญหามลพิษทาง กลิ่น / เสียง<span class=\"rm\">หมายเหตุ : โรงงานข้างเคียง</span></span>"))
            .And.NotContain(FloodLabel).And.NotContain(OtherLabel).And.NotContain(NoneRow);
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task Other_alone_prints_its_remark_as_the_value(string body)
    {
        var html = await RenderBothAsync(body, other: true, otherRemark: "ฝุ่นจากการก่อสร้างข้างเคียง");

        html.Should().Contain(Row("ปัญหาอื่นๆ", "ฝุ่นจากการก่อสร้างข้างเคียง</span>"))
            .And.NotContain(FloodLabel).And.NotContain(PollutionLabel).And.NotContain(NoneRow).And.NotContain(DashRow);
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task All_three_selected_print_three_rows(string body)
    {
        var html = await RenderBothAsync(body, flood: true, pollution: true, other: true, pollutionType: "ฝุ่น", otherRemark: "อื่น");

        html.Should().Contain(FloodLabel).And.Contain(PollutionLabel).And.Contain(OtherLabel).And.NotContain(NoneRow);
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task None_selected_with_an_explicit_false_prints_one_row_saying_none(string body)
    {
        var html = await RenderBothAsync(body, flood: false, pollution: null, other: null);

        html.Should().Contain(NoneRow).And.NotContain(FloodLabel).And.NotContain(PollutionLabel).And.NotContain(OtherLabel);
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task All_null_prints_a_dash_row_and_no_red_row(string body)
    {
        var html = await RenderBothAsync(body);

        html.Should().Contain(DashRow).And.NotContain(NoneRow).And.NotContain("l miss\">ปัญหา");
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task An_empty_remark_prints_no_remark_line(string body)
    {
        var html = await RenderBothAsync(body, flood: true, floodRemark: "");

        html.Should().Contain(Row("ปัญหาเรื่องน้ำท่วมขัง", "มีปัญหาเรื่องน้ำท่วมขัง</span>")).And.NotContain("class=\"rm\"");
    }

    [Theory]
    [InlineData(false, "ตำแหน่งไม่ตรงกับแผนที่", true)]
    [InlineData(false, "   ", false)]
    [InlineData(false, null, false)]
    [InlineData(true, "ตำแหน่งไม่ตรงกับแผนที่", false)]
    [InlineData(null, "ตำแหน่งไม่ตรงกับแผนที่", false)]
    public async Task Land_location_remark_prints_only_when_the_position_is_wrong_and_the_remark_has_text(bool? verified, string? remark, bool shown)
    {
        var html = await RenderLandAsync(new LandSection { IsLandLocationVerified = verified, LandLocationRemark = remark });

        html.Contains("<span class=\"rm\">หมายเหตุ : ตำแหน่งไม่ตรงกับแผนที่</span>").Should().Be(shown);
        if (verified == false)
            html.Should().Contain("<span class=\"v\">ไม่ถูกต้อง"); // the chosen answer prints as before
    }

    [Theory]
    [InlineData(false, "ห้องอยู่คนละชั้นกับแผนผัง", true)]
    [InlineData(false, "  ", false)]
    [InlineData(true, "ห้องอยู่คนละชั้นกับแผนผัง", false)]
    [InlineData(null, "ห้องอยู่คนละชั้นกับแผนผัง", false)]
    public async Task Condo_position_remark_prints_only_when_the_position_is_wrong_and_the_remark_has_text(bool? correct, string? remark, bool shown)
    {
        var condo = new CondoSection { PositionCorrect = correct, PositionRemark = remark };
        var flow = await RenderCondoAsync(condo);
        var mixed = await RenderAsync("{{ include 'partials/book-funcs' }}{{ cd = model.condo_section }}{{ include 'partials/book-mixed-extras' }}",
            new AppraisalSummaryModel { CondoSection = condo });

        foreach (var html in new[] { flow, mixed })
        {
            html.Contains("<span class=\"rm\">หมายเหตุ : ห้องอยู่คนละชั้นกับแผนผัง</span>").Should().Be(shown);
            if (correct == false) html.Should().Contain("<span class=\"v\">ไม่ถูกต้อง");
        }
    }

    [Theory]
    [InlineData("01", null, "แปลงคง")]                                   // a bare code on an older row
    [InlineData("[\"01\",\"02\"]", null, "แปลงคง, ระวาง")]               // several, joined with a comma
    [InlineData("[\"01\",\"99\"]", "ภาพถ่ายดาวเทียม", "แปลงคง, ภาพถ่ายดาวเทียม")] // the other text replaces the 99 label
    [InlineData("99", "ภาพถ่ายดาวเทียม", "ภาพถ่ายดาวเทียม")]
    public void Check_method_codes_decode_to_thai_joined_with_the_other_text(string raw, string? other, string expected)
    {
        var map = new Dictionary<string, string?> { ["01"] = "แปลงคง", ["02"] = "ระวาง", ["99"] = "อื่นๆ" };

        Reporting.Application.Providers.Sections.LandSectionLoader.JsonCodesToThai(raw, map, other).Should().Be(expected);
    }

    [Fact]
    public async Task The_land_book_prints_the_check_methods_as_one_row()
    {
        var html = await RenderLandAsync(new LandSection { CheckedFrom = "แปลงคง, ระวาง" });

        html.Should().Contain("<span class=\"l\">ตรวจสอบจาก</span><span class=\"c\">:</span><span class=\"v\">แปลงคง, ระวาง</span>");
    }

    [Fact]
    public async Task Title_docs_no_longer_print_the_map_sheet_row_and_keep_the_rawang()
    {
        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ multi_land = false }}{{ include 'partials/book-title-docs' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = [new LandSection { LandOffice = "สำนักงานที่ดินตัวอย่าง", Titles = [new LandTitleRow { Rawang = "5137 IV" }] }] }] });

        html.Should().NotContain("แผ่นที่").And.NotContain("<span class=\"l\">สำนักงานที่ดิน</span>")
            .And.Contain("ระวาง 5137 IV"); // the โฉนด ระวาง under the title table stays
    }

    private const string CityPlanLabel = "<span class=\"l\">ผังเมือง</span>";

    private static string CityPlanRow(string value) => $"{CityPlanLabel}<span class=\"c\">:</span><span class=\"v\">{value}</span>";

    [Theory]
    [InlineData("สีแดง", "ย่านที่อยู่อาศัยหนาแน่นมาก", "สีแดง ย่านที่อยู่อาศัยหนาแน่นมาก")]
    [InlineData("สีแดง", null, "สีแดง")]
    [InlineData(null, "ย่านที่อยู่อาศัยหนาแน่นมาก", "ย่านที่อยู่อาศัยหนาแน่นมาก")]
    [InlineData("  ", "ย่านพาณิชยกรรม", "ย่านพาณิชยกรรม")]
    public async Task City_plan_row_is_colour_then_type_or_the_one_part_there_is(string? color, string? type, string expected)
    {
        var land = await RenderLandAsync(new LandSection { UrbanPlanningColor = color, UrbanPlanningType = type });
        var condo = await RenderCondoAsync(new CondoSection { UrbanPlanningColor = color, LandType = type });

        land.Should().Contain(CityPlanRow(expected));
        condo.Should().Contain(CityPlanRow(expected));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "  ")]
    public async Task City_plan_row_is_hidden_when_neither_part_exists(string? color, string? type)
    {
        var land = await RenderLandAsync(new LandSection { UrbanPlanningColor = color, UrbanPlanningType = type });
        var condo = await RenderCondoAsync(new CondoSection { UrbanPlanningColor = color, LandType = type });

        land.Should().NotContain(CityPlanLabel);
        condo.Should().NotContain(CityPlanLabel);
    }

    private static Task<string> RenderTitleDocsAsync(LandSection land) =>
        RenderAsync("{{ include 'partials/book-funcs' }}{{ multi_land = false }}{{ include 'partials/book-title-docs' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = [land] }] });

    private static string Section22(string html)
    {
        var start = html.IndexOf("การรอนสิทธิ</span>", StringComparison.Ordinal);
        return html[start..html.IndexOf("การรอนสิทธิอื่นๆ", start, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task Section_2_2_of_the_construction_book_has_no_encroachment_row_and_forest_is_yes_no_only()
    {
        var yes = Section22(await RenderTitleDocsAsync(new LandSection
        {
            IsEncroached = true, EncroachmentRemark = "รั้วบ้านข้างเคียงล้ำเข้ามา",
            IsLandlocked = true, LandlockedRemark = "ไม่มีทางเข้าออก", RoyalDecree = "พ.ร.ฎ. ตัวอย่าง",
            IsForestBoundary = true, ForestBoundaryRemark = "ติดเขตป่าสงวน"
        }));
        var no = Section22(await RenderTitleDocsAsync(new LandSection
        {
            IsEncroached = false, EncroachmentRemark = "ไม่ควรแสดง", IsForestBoundary = null, ForestBoundaryRemark = "ไม่ควรแสดง"
        }));

        yes.Should().NotContain("การบุกรุก").And.NotContain("รั้วบ้านข้างเคียงล้ำเข้ามา").And.NotContain("(")
            .And.NotContain("ที่ดินตาบอด").And.NotContain("พระราชกฤษฎีกาเวนคืน").And.NotContain("ไม่มีทางเข้าออก").And.NotContain("ติดเขตป่าสงวน").And.NotContain("พ.ร.ฎ.");
        no.Should().NotContain("class=\"rm\"").And.NotContain("ไม่ควรแสดง").And.NotContain("การบุกรุก");
    }

    [Fact]
    public async Task Condo_2_2_prints_expropriation_and_forest_boundary_as_yes_no_only()
    {
        var html = await RenderCondoAsync(new CondoSection
        {
            IsInExpropriationLine = true, RoyalDecree = "พ.ร.ฎ. ตัวอย่าง", IsForestBoundary = true, ForestBoundaryRemark = "ติดเขตป่าสงวน"
        });

        html.Should().Contain("<span class=\"v\">อยู่ในแนวเวนคืน</span>").And.Contain("<span class=\"l\">แนวเขตป่าไม้</span><span class=\"c\">:</span><span class=\"v\">อยู่</span>")
            .And.NotContain("พระราชกฤษฎีกาเวนคืน").And.NotContain("พ.ร.ฎ.").And.NotContain("ติดเขตป่าสงวน");
    }

    [Fact]
    public async Task The_condo_fallback_section_4_is_4_1_to_4_3_with_no_expropriation_block()
    {
        var html = await RenderCondoAsync(new CondoSection { IsInExpropriationLine = true, ExpropriationRemark = "ตามแนวถนนสายใหม่" });
        var s4 = html[html.IndexOf("<span>4.</span>", StringComparison.Ordinal)..];

        s4.Should().Contain("<span>4.1</span>").And.Contain("<span>4.2</span>").And.Contain("<span>4.3</span>")
            .And.NotContain("<span>4.4</span>").And.NotContain("<span>การเวนคืน</span>").And.NotContain("ตามแนวถนนสายใหม่");
    }

    [Fact]
    public async Task Section_1_3_prints_land_accessibility_after_the_entrance_right_with_its_remark_line()
    {
        var html = await RenderLandAsync(new LandSection { LandEntranceExitType = "ทางสาธารณะ", LandAccessibility = "ต้องมีการปรับปรุงสภาพทางเข้า-ออก", LandAccessibilityRemark = "ต้องถมทางเข้า" });

        html.Should().Contain("<span class=\"l\">การเข้าถึงที่ดิน</span><span class=\"c\">:</span><span class=\"v\">ต้องมีการปรับปรุงสภาพทางเข้า-ออก<span class=\"rm\">หมายเหตุ : ต้องถมทางเข้า</span></span>");
        html.IndexOf("การเข้าถึงที่ดิน", StringComparison.Ordinal).Should().BeGreaterThan(html.IndexOf("สิทธิทางเข้าออก", StringComparison.Ordinal))
            .And.BeLessThan(html.IndexOf("การเดินทาง", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Section_1_3_land_accessibility_without_a_remark_has_no_remark_line_and_when_empty_prints_a_dash()
    {
        var plain = await RenderLandAsync(new LandSection { LandAccessibility = "ได้" });
        var empty = await RenderLandAsync(new LandSection());

        plain.Should().Contain("<span class=\"l\">การเข้าถึงที่ดิน</span><span class=\"c\">:</span><span class=\"v\">ได้</span>");
        empty.Should().Contain("<span class=\"l\">การเข้าถึงที่ดิน</span><span class=\"c\">:</span><span class=\"v\">-</span>");
    }

    [Fact]
    public async Task Rows_under_the_title_table_drop_office_owner_and_ownership_check_and_shorten_the_mortgage_label()
    {
        var html = await RenderLandsUnderTitles(new LandSection
        {
            LandOffice = "สำนักงานที่ดินตัวอย่าง", IsOwnerVerified = true, ObligationMortgage = "จำนองเป็นประกัน", ObligationOthers = "ติดสัญญาเช่า", Remark = "หมายเหตุตัวอย่าง",
            Titles = [new LandTitleRow { TitleNumber = "12345", OwnerName = "นายตัวอย่าง" }]
        });

        foreach (var removed in new[] { "สำนักงานที่ดิน", "ผู้ถือกรรมสิทธิ์", "ตรวจสอบกรรมสิทธิ์", "ภาระผูกพันการจำนอง" })
            html.Should().NotContain($"<span class=\"l\">{removed}</span>");
        html.Should().Contain("<span class=\"l\">ภาระผูกพัน</span><span class=\"c\">:</span><span class=\"v\">จำนองเป็นประกัน</span>")
            .And.Contain("<span class=\"l\">ภาระผูกพันอื่นๆ</span>").And.Contain("<span class=\"l\">หมายเหตุ</span>");
    }

    [Fact]
    public async Task Condo_rows_under_the_title_table_drop_office_owner_and_ownership_check_and_shorten_the_mortgage_label()
    {
        var html = await RenderCondoAsync(new CondoSection
        {
            LandOffice = "สำนักงานที่ดินตัวอย่าง", OwnerName = "นายตัวอย่าง", IsOwnerVerified = true,
            Units = [new CondoUnitAreaRow { RoomNumber = "101", ObligationMortgage = "จำนองเป็นประกัน", ObligationOthers = "ติดสัญญาเช่า" }],
            BuiltOnTitleNumber = "999", IsRentedOut = false
        });

        foreach (var removed in new[] { "สำนักงานที่ดิน", "ผู้ถือกรรมสิทธิ์", "ตรวจสอบกรรมสิทธิ์", "ภาระผูกพันการจำนอง" })
            html.Should().NotContain($"<span class=\"l\">{removed}</span>");
        html.Should().Contain("<span class=\"l\">ภาระผูกพัน</span><span class=\"c\">:</span><span class=\"v\">จำนองเป็นประกัน</span>")
            .And.Contain("<span class=\"l\">ปลูกสร้างบนโฉนดที่ดินเลขที่</span>").And.Contain("<span class=\"l\">ที่ตั้งเอกสารสิทธิ์</span>")
            .And.Contain("<span class=\"l\">ภาระผูกพันอื่นๆ</span>").And.Contain("<span class=\"l\">การให้เช่า</span>");
    }

    private static Task<string> RenderLandsUnderTitles(LandSection land) =>
        RenderAsync("{{ include 'partials/book-funcs' }}{{ multi_land = false }}{{ include 'partials/book-title-docs' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = [land] }] });

    [Fact]
    public async Task Road_surface_prints_the_decoded_value_as_text_and_a_dash_when_empty()
    {
        var withValue = await RenderLandAsync(new LandSection { RoadSurfaceType = "ลาดยาง" });
        var none = await RenderLandAsync(new LandSection());

        withValue.Should().Contain("<span class=\"l\">ลักษณะผิวจราจร</span><span class=\"c\">:</span><span class=\"v\">ลาดยาง</span>").And.NotContain("คอนกรีต");
        none.Should().Contain("<span class=\"l\">ลักษณะผิวจราจร</span><span class=\"c\">:</span><span class=\"v\">-</span>");
    }

    [Fact]
    public async Task Soil_level_prints_in_metres_relative_to_the_road()
    {
        var html = await RenderAsync("{{ include 'partials/book-flow-standard' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = [new LandSection { SoilLevel = 0.2m }], Buildings = [] }] });

        html.Should().Contain("<span class=\"l\">ระดับดิน</span>").And.Contain("ม. เทียบกับถนน").And.NotContain("ซม. เทียบกับถนน");
    }

    private static Task<string> RenderStandardAsync(params LandSection[] lands) =>
        RenderAsync("{{ include 'partials/book-flow-standard' }}", new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = lands, Buildings = [] }] });

    [Fact]
    public async Task Land_physical_description_is_a_sub_headed_paragraph_not_a_row_and_keeps_line_breaks()
    {
        var html = await RenderStandardAsync(new LandSection { LandDescriptionText = "ที่ดินรูปสี่เหลี่ยมผืนผ้า\nติดถนนคอนกรีตด้านทิศเหนือ" });

        html.Should().Contain("<div class=\"vk-sub\">ลักษณะทางกายภาพ</div>")
            .And.Contain("white-space:pre-line;overflow-wrap:anywhere;line-height:1.75;font-size:10.5pt;padding-left:14px;\">ที่ดินรูปสี่เหลี่ยมผืนผ้า\nติดถนนคอนกรีตด้านทิศเหนือ</div>")
            .And.NotContain("<span class=\"l\">ลักษณะทางกายภาพ</span>"); // no longer a label : value row
        html.IndexOf("<div class=\"vk-sub\">ลักษณะทางกายภาพ", StringComparison.Ordinal)
            .Should().BeGreaterThan(html.IndexOf("การให้เช่า", StringComparison.Ordinal)); // after the vk block
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Land_physical_description_prints_nothing_when_blank(string? text)
    {
        var html = await RenderStandardAsync(new LandSection { LandDescriptionText = text });

        html.Should().NotContain("ลักษณะทางกายภาพ");
    }

    [Fact]
    public async Task Each_land_prints_its_own_physical_description()
    {
        var html = await RenderStandardAsync(new LandSection { LandDescriptionText = "แปลงแรก" }, new LandSection { LandDescriptionText = "แปลงที่สอง" });

        html.Split("<div class=\"vk-sub\">ลักษณะทางกายภาพ</div>").Length.Should().Be(3);
        html.IndexOf("แปลงแรก", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("แปลงที่สอง", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Other_legal_limitations_is_the_last_sub_headed_paragraph_of_3_1_after_the_physical_description()
    {
        var html = await RenderStandardAsync(new LandSection { LandDescriptionText = "รูปสี่เหลี่ยม", OtherLegalLimitations = "มีสายไฟฟ้าแรงสูงพาดผ่าน\nอยู่ในเขตปลอดภัย" });

        html.Should().Contain("<div class=\"vk-sub\">ข้อจำกัดอื่นๆ ที่พบ</div>")
            .And.Contain("white-space:pre-line;overflow-wrap:anywhere;line-height:1.75;font-size:10.5pt;padding-left:14px;\">มีสายไฟฟ้าแรงสูงพาดผ่าน\nอยู่ในเขตปลอดภัย</div>")
            .And.NotContain("<span class=\"l\">การรอนสิทธิอื่นๆ");
        html.IndexOf("ข้อจำกัดอื่นๆ ที่พบ", StringComparison.Ordinal).Should().BeGreaterThan(html.IndexOf("ลักษณะทางกายภาพ", StringComparison.Ordinal));
        html.IndexOf("ข้อจำกัดอื่นๆ ที่พบ", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("<span>4.</span>", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Other_legal_limitations_prints_nothing_when_blank(string? text)
    {
        var html = await RenderStandardAsync(new LandSection { OtherLegalLimitations = text });

        html.Should().NotContain("ข้อจำกัดอื่นๆ ที่พบ");
    }

    private static readonly string[] RoadRows = ["ถนนผ่านหน้า", "ลักษณะผิวจราจร", "จำนวนช่องจราจร", "ผิวจราจรกว้าง", "เขตทางกว้าง", "หน้าที่ดินติดถนนกว้าง"];

    private static List<int> Positions(string html, IEnumerable<string> labels) =>
        labels.Select(l => html.IndexOf($"<span class=\"l\">{l}", StringComparison.Ordinal)).ToList();

    [Fact]
    public async Task Land_road_rows_put_the_lane_count_directly_under_the_road_surface()
    {
        var html = await RenderLandAsync(new LandSection { RoadSurfaceType = "ลาดยาง", NumberOfLanes = 2 });

        var rows = RoadRows.Where(r => r != "ถนนผ่านหน้า").ToList();
        var positions = Positions(html, rows.Prepend("ถนนผ่านหน้าทรัพย์สิน"));
        positions.Should().OnlyContain(p => p >= 0).And.BeInAscendingOrder();
    }

    [Fact]
    public async Task Condo_road_rows_put_the_lane_count_directly_under_the_road_surface()
    {
        var html = await RenderCondoAsync(new CondoSection { RoadSurfaceType = "ลาดยาง", NumberOfLanes = 2 });

        Positions(html, ["ลักษณะผิวจราจร", "จำนวนช่องจราจร", "ผิวจราจรกว้าง", "เขตทางกว้าง"]).Should().OnlyContain(p => p >= 0).And.BeInAscendingOrder();
    }

    [Fact]
    public async Task Land_environment_row_prints_the_environment_and_not_the_zoning_type()
    {
        var html = await RenderLandAsync(new LandSection { EnvironmentType = "ย่านพาณิชยกรรม, สวนผลไม้", UrbanPlanningType = "ที่ดินประเภทผังเมือง X" });

        // the zoning type is no longer the สภาพแวดล้อม value; it prints on the ผังเมือง row
        html.Should().Contain("<span class=\"l\">สภาพแวดล้อม</span><span class=\"c\">:</span><span class=\"v\">ย่านพาณิชยกรรม, สวนผลไม้</span>")
            .And.Contain("<span class=\"l\">ผังเมือง</span><span class=\"c\">:</span><span class=\"v\">ที่ดินประเภทผังเมือง X</span>");
    }

    [Fact]
    public async Task Land_environment_row_is_a_dash_on_records_without_it()
    {
        var html = await RenderLandAsync(new LandSection { UrbanPlanningType = "ที่ดินประเภทผังเมือง X" });

        html.Should().Contain("<span class=\"l\">สภาพแวดล้อม</span><span class=\"c\">:</span><span class=\"v\">-</span>");
    }

    [Fact]
    public void Environment_codes_decode_with_the_other_text_in_place_of_code_99()
    {
        var map = new Dictionary<string, string?> { ["06"] = "ย่านพาณิชยกรรม", ["99"] = "อื่นๆ" };

        Reporting.Application.Providers.Sections.LandSectionLoader.JsonCodesToThai("[\"06\",\"99\"]", map, "สวนผลไม้")
            .Should().Be("ย่านพาณิชยกรรม, สวนผลไม้");
    }

    private static LandSection Land31(decimal? pondArea = null, decimal? pondDepth = null) => new()
    {
        LandDescription = "สี่เหลี่ยมผืนผ้า", LandUseType = "ที่อยู่อาศัย", IsRentedOut = true,
        PondArea = pondArea, PondDepth = pondDepth,
        Boundaries = [new LandBoundaryRow { Direction = "ทิศเหนือ", Distance = 20m, Adjacent = "ที่ดินแปลงข้างเคียง" }],
        LandDescriptionText = "ที่ดินรูปสี่เหลี่ยมผืนผ้า ติดถนน"
    };

    [Fact]
    public async Task Land_3_1_shows_the_pond_rows_with_units_when_there_is_a_pond()
    {
        var html = await RenderStandardAsync(Land31(pondArea: 12.5m, pondDepth: 2m));

        html.Should().Contain("<span class=\"l\">เนื้อที่บ่อน้ำ</span>").And.Contain("12.5").And.Contain("ตารางวา")
            .And.Contain("<span class=\"l\">ความลึกบ่อน้ำ</span>").And.Contain("เมตร");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, 0.0)]
    public async Task Land_3_1_prints_no_pond_rows_when_both_are_empty_or_zero(double? area, double? depth)
    {
        var html = await RenderStandardAsync(Land31((decimal?)area, (decimal?)depth));

        html.Should().NotContain("เนื้อที่บ่อน้ำ").And.NotContain("ความลึกบ่อน้ำ");
    }

    [Fact]
    public async Task Land_3_1_has_no_rented_out_and_no_current_use_row_and_orders_rows_then_physical_description_then_boundary()
    {
        var html = await RenderStandardAsync(Land31(pondArea: 5m));

        html.Should().NotContain("การให้เช่า").And.NotContain("<span class=\"l\">การใช้ประโยชน์ปัจจุบัน</span>"); // 1.2 prints the land use
        new[]
        {
            html.IndexOf("<span class=\"l\">เนื้อที่บ่อน้ำ</span>", StringComparison.Ordinal),
            html.IndexOf("<div class=\"vk-sub\">ลักษณะทางกายภาพ</div>", StringComparison.Ordinal),
            html.IndexOf("<div class=\"vk-sub\">อาณาเขต</div>", StringComparison.Ordinal),
            html.IndexOf("<th style=\"width:24%;\">ทิศ</th>", StringComparison.Ordinal)
        }.Should().OnlyContain(i => i >= 0).And.BeInAscendingOrder();
    }

    [Fact]
    public async Task Land_3_1_has_no_boundary_sub_header_without_boundaries()
    {
        var html = await RenderStandardAsync(new LandSection { LandDescription = "สี่เหลี่ยมผืนผ้า" });

        html.Should().NotContain("อาณาเขต");
    }

    [Theory]
    [InlineData(100, "ถมแล้ว")]          // fully filled: no percent
    [InlineData(60, "ถมแล้ว (60%)")]
    [InlineData(0, "ถมแล้ว")]
    [InlineData(null, "ถมแล้ว")]
    public async Task Land_fill_prints_its_percent_only_when_it_is_not_100(int? percent, string expected)
    {
        var html = await RenderStandardAsync(new LandSection { LandFill = "ถมแล้ว", LandFillPercent = percent });

        html.Should().Contain($"<span class=\"l\">สภาพที่ดิน</span><span class=\"c\">:</span><span class=\"v\">{expected}</span>");
    }

    [Fact]
    public async Task Gps_prints_six_decimals_even_for_whole_numbers()
    {
        var html = await RenderAsync(
            "{{ include 'partials/book-funcs' }}{{ gps 13 100 }}|{{ gps 13.5 100.123456789 }}",
            new AppraisalSummaryModel());

        html.Should().Contain("N 13.000000 E 100.000000").And.Contain("N 13.500000 E 100.123457");
    }

    [Fact]
    public async Task Vk_drops_the_unit_when_the_value_is_empty()
    {
        var html = await RenderAsync(
            "{{ include 'partials/book-funcs' }}{{ vk 'อายุอาคารประมาณ' null 'ปี' }}{{ vk 'พื้นที่' 5 'ตารางเมตร' }}",
            new AppraisalSummaryModel());

        html.Should().NotContain("<span>ปี</span>").And.Contain("<span>ตารางเมตร</span>");
    }

    [Fact]
    public void Code_99_prints_the_other_text_in_place_and_joins_with_the_separator()
    {
        var map = new Dictionary<string, string?> { ["01"] = "กลิ่น", ["99"] = "อื่นๆ" };

        LandSectionLoader.JsonCodesToThai("[\"01\",\"99\"]", map, "แสงรบกวน", " / ").Should().Be("กลิ่น / แสงรบกวน");
        LandSectionLoader.JsonCodesToThai("[\"01\"]", map).Should().Be("กลิ่น");
        LandSectionLoader.JsonCodesToThai("[\"99\"]", map).Should().Be("อื่นๆ");
        LandSectionLoader.JsonCodesToThai(null, map).Should().BeNull();
    }

    [Fact]
    public void Multi_select_codes_print_in_parameter_order_with_other_last()
    {
        var map = new Dictionary<string, string?> { ["01"] = "กลิ่น", ["02"] = "เสียง", ["99"] = "อื่นๆ" };

        // ticked 99 first, then 02, then 01
        LandSectionLoader.JsonCodesToThai("[\"99\",\"02\",\"01\"]", map, "แสงรบกวน", " / ").Should().Be("กลิ่น / เสียง / แสงรบกวน");
        AppraisalSummaryCommonLoader.OrderCodes(["99", "10", "02", "01"]).Should().Equal("01", "02", "10", "99");
    }

    [Fact]
    public async Task Road_rows_print_right_of_way_width_number_of_lanes_and_carriageway_width_for_land_and_condo()
    {
        var land = await RenderLandAsync(new LandSection { RightOfWayWidth = 12.5m, NumberOfLanes = 2, CarriagewayWidth = 6m });
        var condo = await RenderCondoAsync(new CondoSection { RightOfWayWidth = 12.5m, NumberOfLanes = 2, CarriagewayWidth = 6m });

        foreach (var html in new[] { land, condo })
        {
            html.Should().Contain("<span>12.50</span><span>เมตร</span>").And.Contain("<span>6.00</span><span>เมตร</span>")
                .And.Contain("<span>2</span><span>ช่องจราจร</span>").And.NotContain("l miss\">เขตทางกว้าง");
        }
    }
}

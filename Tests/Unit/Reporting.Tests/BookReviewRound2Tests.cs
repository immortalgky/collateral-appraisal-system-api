using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Application.Providers.Sections;
using Reporting.Infrastructure.PdfAssembly;
using Reporting.Infrastructure.Rendering;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>Code-review round 2 on the appraisal book (items numbered as in the review).</summary>
public class BookReviewRound2Tests
{
    private static DataTable Empty() { var t = new DataTable(); t.Columns.Add("Unused", typeof(int)); return t; }

    // ── 1. the condo header's obligations are every unit's own ──────────────────────────────────────────────────────

    private static CondoSection Units(params (string Room, string Mortgage, string Others)[] units) => new()
    {
        ObligationMortgage = "ไม่มี", ObligationOthers = "ไม่มี",
        Units = units.Select(u => new CondoUnitAreaRow { RoomNumber = u.Room, ObligationMortgage = u.Mortgage, ObligationOthers = u.Others }).ToList()
    };

    [Fact]
    public void Units_alike_give_one_text_and_units_that_differ_give_one_line_per_unit_that_has_one()
    {
        Units(("101", "จำนองกับธนาคาร ก", "ไม่มี"), ("102", "จำนองกับธนาคาร ก", "ไม่มี")).ObligationMortgageText.Should().Be("จำนองกับธนาคาร ก");
        Units(("101", "ไม่มี", "ไม่มี"), ("102", "ไม่มี", "ไม่มี")).ObligationMortgageText.Should().Be("ไม่มี");

        var mixed = Units(("101", "ไม่มี", "ไม่มี"), ("102", "จำนองกับธนาคาร ข", "ไม่มี"), ("103", "จำนองกับธนาคาร ค", "ไม่มี"));
        mixed.ObligationMortgageText.Should().Be("ห้อง 102: จำนองกับธนาคาร ข\nห้อง 103: จำนองกับธนาคาร ค");
        mixed.ObligationOthersText.Should().Be("ไม่มี");

        // one unit with one, the others with none: still paired, a lone value would read as if it covered every room
        Units(("101", "ไม่มี", "ไม่มี"), ("102", "จำนองกับธนาคาร ข", "ไม่มี")).ObligationMortgageText.Should().Be("ห้อง 102: จำนองกับธนาคาร ข");
    }

    [Fact]
    public async Task The_condo_loader_fills_each_units_own_obligations()
    {
        var prms = new DataTable();
        foreach (var n in new[] { "Group", "Code", "Description" }) prms.Columns.Add(n, typeof(string));
        var detail = new DataTable();
        detail.Columns.Add("Id", typeof(Guid)); detail.Columns.Add("RoomNumber", typeof(string)); detail.Columns.Add("ObligationType", typeof(string)); detail.Columns.Add("MortgageDetails", typeof(string));
        detail.Rows.Add(Guid.NewGuid(), "101", "[\"01\"]", null);
        detail.Rows.Add(Guid.NewGuid(), "102", "[\"02\"]", "จำนองกับธนาคาร ข");
        var ds = new DataSet();
        ds.Tables.Add(prms); ds.Tables.Add(detail); ds.Tables.Add(Empty()); ds.Tables.Add(Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);

        var condo = await CondoSectionLoader.LoadAsync(f.GetOpenConnection(), Guid.NewGuid());

        condo!.Units.Select(u => u.ObligationMortgage).Should().Equal("ไม่มี", "จำนองกับธนาคาร ข");
        condo.ObligationMortgageText.Should().Be("ห้อง 102: จำนองกับธนาคาร ข");
    }

    // ── 2. ส่วนพัฒนา alone on the cover ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_property_with_only_development_items_is_named_on_the_cover_as_the_summary_page_names_it()
    {
        var m = new AppraisalSummaryModel
        {
            BodyType = "standard", CollateralAddress = "เลขที่ 1 ตำบลตัวอย่าง",
            PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Buildings = [new BuildingSection { IsDevelopmentOnly = true, DevelopmentItems = [new DevelopmentItem { AreaDescription = "รั้ว" }] }] }]
        };

        CoverNarrative.Build(m).Should().Contain(l => l.Contains("ส่วนพัฒนาอื่นๆ")).And.NotContain(l => l.Contains("สิ่งปลูกสร้าง จำนวน"));
        ExternalSummaryRows.PropertyBlocks(m).Should().Contain(b => b.Lines[0].Contains("ส่วนพัฒนาอื่นๆ"));
    }

    // ── 3. a gallery file path is a fallback only when there is no document row at all ──────────────────────────────

    [Fact]
    public async Task The_appendix_and_photo_queries_drop_a_photo_whose_document_is_deleted_instead_of_falling_back_to_its_gallery_path()
    {
        var f = new FakeSqlConnectionFactory();
        f.EnqueueResult(Empty()); f.EnqueueResult(Empty());
        await AppendixSectionLoader.LoadAsync(f.GetOpenConnection(), Guid.NewGuid());

        foreach (var sql in f.Commands)
        {
            sql.Should().Contain("IsActive = 1 AND").And.NotMatchRegex("ON d[dg]?\\.Id = [a-z.]*DocumentId\\s+AND");   // the activity test is in WHERE, not in the join
            sql.Should().Contain(".Id IS NULL OR (");
        }
        f.Commands.Should().HaveCountGreaterThan(0);
    }

    // ── 4. a crop box is measured from the media box's own origin ───────────────────────────────────────────────────

    [Fact]
    public void The_crop_offset_is_measured_from_the_media_boxs_own_corner()
    {
        var media = new PdfRectangle(new XRect(100, 100, 595, 842));   // origin (100,100): x 100..695, y 100..942
        var crop = new PdfRectangle(new XRect(150, 150, 300, 400));    // x 150..450, y 150..550

        var box = PdfSharpAssembler.VisibleBox(crop, media)!.Value;

        (box.X, box.Y, box.W, box.H).Should().Be((50, 392, 300, 400));   // 150-100 from the left, 942-550 from the top
    }

    [Fact]
    public void A_crop_box_sticking_out_of_the_media_box_shows_only_what_is_inside_it()
    {
        var media = new PdfRectangle(new XRect(0, 0, 595, 842));
        var crop = new PdfRectangle(new XRect(-50, 700, 400, 300));    // x -50..350, y 700..1000: left and top stick out

        var box = PdfSharpAssembler.VisibleBox(crop, media)!.Value;

        (box.X, box.Y, box.W, box.H).Should().Be((0, 0, 350, 142));
        PdfSharpAssembler.VisibleBox(new PdfRectangle(new XRect(700, 0, 50, 50)), media).Should().BeNull();   // wholly outside
        PdfSharpAssembler.VisibleBox(null, media).Should().BeNull();
    }

    // ── 5. the sub-analysis query has a deterministic order ────────────────────────────────────────────────────────

    [Fact]
    public async Task The_sub_analysis_query_orders_analyses_and_approaches_by_creation_not_by_guid()
    {
        var f = new FakeSqlConnectionFactory();
        f.EnqueueResult(Empty());
        await PricingSubAnalysisLoader.LoadForHostsAsync(f.GetOpenConnection(), [Guid.NewGuid()], d => d, CancellationToken.None);

        f.Commands.Should().ContainSingle().Which.Should().Contain($"pa.AnchorRefKey, {BookReviewRound1Tests.NullsLast("pa.CreatedAt")}, pa.Id,")
            .And.Contain($"paa.IsSelected DESC, {BookReviewRound1Tests.NullsLast("paa.CreatedAt")}, paa.ApproachType, paa.Id,")
            .And.Contain($"{BookReviewRound1Tests.NullsLast("pam.CreatedAt")}, pam.MethodType, pam.Id");
    }

    // ── 6. the land product uses the rate pricing multiplies by ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(50_000, 40_000, 100, 5_000_000)]       // the typed-over rate wins
    [InlineData(null, 40_000, 100, 4_000_000)]         // else the calculated one
    [InlineData(null, null, 100, null)]
    public void The_land_product_multiplies_by_the_override_else_the_calculated_rate(int? over, int? calculated, int area, int? expected) =>
        AppraisalSummaryLandBuildingDataProvider.LandProduct(true, true, 1m, area, over, calculated).Should().Be(expected);

    [Fact]
    public void The_land_product_is_null_when_not_per_area_or_the_area_is_excluded()
    {
        AppraisalSummaryLandBuildingDataProvider.LandProduct(false, true, 1m, 100, 1m, null).Should().BeNull();
        AppraisalSummaryLandBuildingDataProvider.LandProduct(true, false, 1m, 100, 1m, null).Should().BeNull();
    }

    // ── 7. a land with no numbered deed has its own owner and eviction item ─────────────────────────────────────────

    private static LandSection Land(string? title, string owner, string? eviction = null) => new()
    {
        EvictionType = eviction,
        Titles = [new LandTitleRow { TitleTypeDisplay = "โฉนดที่ดิน", TitleNumber = title, OwnerName = owner }]
    };

    [Fact]
    public void Owners_and_evictions_name_a_land_without_a_numbered_deed_as_its_own_item()
    {
        var m = new AppraisalSummaryModel
        {
            BodyType = "standard",
            PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = [Land("12345", "นายเอ", "ไม่มี"), Land(null, "นายบี", "ถูกรอนสิทธิ")] }]
        };

        LetterOwnerRows.LandOwners(m).Should().Be("โฉนดที่ดิน 12345: นายเอ\nที่ดินแปลงที่ 2: นายบี");
        LetterOwnerRows.LandEvictions(m).Should().Contain("ที่ดินแปลงที่ 2");
    }

    [Fact]
    public void Lands_none_of_which_has_a_numbered_deed_still_print_their_owners()
    {
        var m = new AppraisalSummaryModel { BodyType = "standard", PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = [Land(null, "นายเอ"), Land(" ", "นายบี")] }] };

        LetterOwnerRows.LandOwners(m).Should().Be("ที่ดินแปลงที่ 1: นายเอ\nที่ดินแปลงที่ 2: นายบี");
    }
}

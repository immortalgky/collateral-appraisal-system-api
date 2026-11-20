using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>
/// Renders the block-book and condo section partials through the app's Scriban renderer: a field the
/// system does not store prints the red "-" marker, a stored one prints its value.
/// </summary>
public class BookTypesTemplateTests
{
    private const string Na = "<span class=\"na\">-</span>";

    private static Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync(template, model, CancellationToken.None);

    [Fact]
    public async Task Block_front_prints_values_and_red_dash_for_fields_the_system_lacks()
    {
        var model = new AppraisalSummaryModel
        {
            BlockSection = new BlockBookSection
            {
                Land = new BlockLandDetail { Titles = ["โฉนดที่ดินเลขที่ 50000"], RoadInFront = "ลาดยาง" },
                Project = new BlockProjectDetail { ProjectName = "โครงการตัวอย่าง", AppraisedUnitCount = 3 },
                Models = [new BlockModelDetail { Sequence = 1, ModelName = "TH-A", Roof = "กระเบื้อง" }]
            }
        };

        var html = await RenderAsync("{{ include 'partials/section-block-front' }}", model);

        html.Should().Contain("โฉนดที่ดินเลขที่ 50000").And.Contain("โครงการตัวอย่าง").And.Contain("กระเบื้อง");
        // ถนนสายหลัก, โครงการรัฐ, สีผังเมือง, กฎหมายควบคุมอาคาร + 4 project rows + 3 model rows
        html.Split(Na).Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task Block_environment_row_prints_the_environment_and_not_the_zoning_type()
    {
        var model = new AppraisalSummaryModel
        {
            BlockSection = new BlockBookSection
            {
                Land = new BlockLandDetail { Environment = "ย่านพาณิชยกรรม, ริมแม่น้ำ", UrbanPlanning = "ที่ดินประเภทผังเมือง X" }
            }
        };
        var old = new AppraisalSummaryModel { BlockSection = new BlockBookSection { Land = new BlockLandDetail { UrbanPlanning = "ที่ดินประเภทผังเมือง X" } } };

        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ include 'partials/book-flow-block' }}", model);
        var oldHtml = await RenderAsync("{{ include 'partials/book-funcs' }}{{ include 'partials/book-flow-block' }}", old);

        html.Should().Contain("<span class=\"l\">สภาพแวดล้อม</span><span class=\"c\">:</span><span class=\"v\">ย่านพาณิชยกรรม, ริมแม่น้ำ</span>");
        // the zoning type still prints on its own ผังเมือง row only
        html.Should().Contain("<span class=\"l\">ผังเมือง</span><span class=\"c\">:</span><span class=\"v\">ที่ดินประเภทผังเมือง X</span>");
        oldHtml.Should().Contain("<span class=\"l\">สภาพแวดล้อม</span><span class=\"c\">:</span><span class=\"v\">-</span>");
    }

    [Theory]
    [InlineData("แนวท่อส่งน้ำมัน", "ผ่านกลางที่ดิน", "<span class=\"v\">แนวท่อส่งน้ำมัน<span class=\"rm\">หมายเหตุ : ผ่านกลางที่ดิน</span></span>")]
    [InlineData("แนวท่อส่งน้ำมัน", "  ", "<span class=\"v\">แนวท่อส่งน้ำมัน</span>")]
    [InlineData(null, "ผ่านกลางที่ดิน", "<span class=\"v\"><span class=\"rm\">หมายเหตุ : ผ่านกลางที่ดิน</span></span>")]
    public async Task Block_eviction_row_prints_the_remark_line_only_when_it_has_text(string? eviction, string? remark, string expected)
    {
        var model = new AppraisalSummaryModel { BlockSection = new BlockBookSection { Land = new BlockLandDetail { Eviction = eviction, EvictionRemark = remark } } };

        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ include 'partials/book-flow-block' }}", model);

        html.Should().Contain("<span class=\"l\">การรอนสิทธิ</span><span class=\"c\">:</span>" + expected);
    }

    [Fact]
    public async Task Block_back_prints_unit_tables_for_external_books_only()
    {
        var section = new BlockBookSection
        {
            IsCondo = true,
            CondoUnits = [new BlockCondoUnitRow { Sequence = 1, CondoRegistrationNumber = "881/1", RoomNumber = "05A04" }]
        };

        var external = await RenderAsync("{{ include 'partials/section-block-back' }}",
            new AppraisalSummaryModel { BlockSection = section, IsExternal = true });
        var internalBook = await RenderAsync("{{ include 'partials/section-block-back' }}",
            new AppraisalSummaryModel { BlockSection = section, IsExternal = false });

        external.Should().Contain("881/1").And.Contain("05A04");
        internalBook.Should().NotContain("881/1");
    }

    [Fact]
    public async Task Condo_prints_red_dash_for_wall_ceiling_door_window_sanitary_and_road_zone()
    {
        var model = new AppraisalSummaryModel
        {
            CondoSection = new CondoSection { CondoName = "คอนโดตัวอย่าง", Facilities = "ลิฟต์", Environment = "ย่านพาณิชยกรรม" }
        };

        var html = await RenderAsync("{{ include 'partials/section-condo' }}", model);

        // FSD §3 prints only the FSD rows: facilities / environment are loaded but not printed.
        html.Should().Contain("คอนโดตัวอย่าง").And.NotContain("ลิฟต์");
        // เนื้อที่ ผนัง ฝ้าเพดาน ประตู หน้าต่าง สุขภัณฑ์ เป็นพื้นที่สี (เขตทางกว้าง และเป็นพื้นที่สี มีคอลัมน์แล้ว)
        html.Split(Na).Length.Should().Be(7);
    }
}

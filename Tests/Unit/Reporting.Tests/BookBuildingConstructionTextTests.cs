using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using Reporting.Infrastructure.Rendering;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>Book 3.2: a building still under construction says so after its item text, with the progress when it is known.</summary>
public class BookBuildingConstructionTextTests
{
    [Theory]
    [InlineData(null, 60.0, null)]                        // not flagged: nothing, whatever progress is stored
    [InlineData(false, 60.0, null)]
    [InlineData(true, 60.0, "(กำลังก่อสร้าง 60%)")]
    [InlineData(true, 59.5, "(กำลังก่อสร้าง 59.5%)")]     // the figure as stored, trailing zeros dropped
    [InlineData(true, 60.4, "(กำลังก่อสร้าง 60.4%)")]
    [InlineData(true, 65.0, "(กำลังก่อสร้าง 65%)")]
    [InlineData(true, 59.456, "(กำลังก่อสร้าง 59.45%)")]   // more than 2 decimals are cut, not rounded
    [InlineData(true, 0.0, "(กำลังก่อสร้าง 0%)")]          // a keyed 0 is a figure
    [InlineData(true, 99.6, "(กำลังก่อสร้าง 99.6%)")]
    [InlineData(true, 99.99, "(กำลังก่อสร้าง 99.99%)")]
    [InlineData(true, 99.9999, "(กำลังก่อสร้าง 99.99%)")]  // the column holds 4 decimals: still unfinished, never "100%"
    [InlineData(true, 100.0, null)]                       // flagged but already complete: a finished building, as in the summary form
    [InlineData(true, null, "(กำลังก่อสร้าง)")]          // no inspection or no summary figure: never a made-up 0%
    public void Under_construction_text_has_three_states(bool? underConstruction, double? pct, string? expected)
        => new BuildingSection { IsUnderConstruction = underConstruction, ConstructionProgressPct = (decimal?)pct }.UnderConstructionText.Should().Be(expected);

    [Fact]
    public async Task The_item_cell_carries_the_suffix_after_the_item_text_and_no_column_is_added()
    {
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ include 'partials/book-flow-standard' }}",
            new AppraisalSummaryModel
            {
                PropertyGroups = [new PropertyGroupDetail { Lands = [new LandSection()], Buildings =
                [
                    new BuildingSection { HouseNumber = "10/1", BuildingTypeText = "บ้านเดี่ยว", NumberOfFloors = 3, UsableArea = 10m, IsUnderConstruction = true, ConstructionProgressPct = 60m },
                    new BuildingSection { HouseNumber = "10/2", BuildingTypeText = "ทาวน์เฮ้าส์", NumberOfFloors = 2, UsableArea = 10m, IsUnderConstruction = true },
                    new BuildingSection { HouseNumber = "10/3", BuildingTypeText = "โกดัง", NumberOfFloors = 1, UsableArea = 10m }
                ] }]
            }, CancellationToken.None);

        html.Should().Contain("<td>บ้านเดี่ยว 3 ชั้น (กำลังก่อสร้าง 60%)</td>")
            .And.Contain("<td>ทาวน์เฮ้าส์ 2 ชั้น (กำลังก่อสร้าง)</td>")
            .And.Contain("<td>โกดัง 1 ชั้น</td>")
            .And.NotContain("(กำลังก่อสร้าง 0%)");
        var at = html.IndexOf("บ้านเลขที่</th>", StringComparison.Ordinal);
        var head = html[html.LastIndexOf("<thead>", at, StringComparison.Ordinal)..html.IndexOf("</thead>", at, StringComparison.Ordinal)];
        System.Text.RegularExpressions.Regex.Matches(head, "<th[ >]").Count.Should().Be(6); // ลำดับ, บ้านเลขที่, รายการ, พื้นที่, โฉนด, ผู้ถือกรรมสิทธิ์: no column was added
    }

    [Fact]
    public async Task The_loader_maps_the_flag_and_the_progress_from_the_row()
    {
        DataTable T(params (string, Type)[] cols) { var t = new DataTable(); foreach (var (n, ty) in cols) t.Columns.Add(n, ty); return t; }
        var header = T(("BuildingDetailId", typeof(Guid)), ("GroupNumber", typeof(int)), ("HouseNumber", typeof(string)), ("IsUnderConstruction", typeof(bool)), ("ConstructionProgressPct", typeof(decimal)));
        header.Rows.Add(Guid.NewGuid(), 1, "1", true, 45m);
        header.Rows.Add(Guid.NewGuid(), 1, "2", true, DBNull.Value);
        var sets = new DataSet();
        sets.Tables.Add(header);
        sets.Tables.Add(T(("DetailId", typeof(Guid)), ("BuildingAppraisalDetailId", typeof(Guid))));
        sets.Tables.Add(T(("Group", typeof(string)), ("Code", typeof(string)), ("Description", typeof(string))));
        sets.Tables.Add(T(("BuildingAppraisalDetailId", typeof(Guid)), ("FromFloorNumber", typeof(int)), ("ToFloorNumber", typeof(int))));
        sets.Tables.Add(T(("BuildingDetailId", typeof(Guid)), ("FilePath", typeof(string))));
        sets.Tables.Add(T(("DetailId", typeof(Guid)), ("AtYear", typeof(int)), ("ToYear", typeof(int)), ("DepreciationPerYear", typeof(decimal))));
        var factory = new FakeSqlConnectionFactory();
        factory.SetMultiResult(sets);

        var buildings = await BuildingSectionLoader.LoadAllAsync(factory.GetOpenConnection(), Guid.NewGuid());

        buildings.Select(b => b.UnderConstructionText).Should().Equal("(กำลังก่อสร้าง 45%)", "(กำลังก่อสร้าง)");
        // a photo whose document was deleted or deactivated is dropped; the gallery path is a fallback only for a photo with no document row at all
        factory.Commands.Should().Contain(c => c.Contains("AND (d.Id IS NULL OR (d.IsActive = 1 AND d.IsDeleted = 0))") && c.Contains("LEFT JOIN document.Documents d ON d.Id = g.DocumentId\n"));
    }
}

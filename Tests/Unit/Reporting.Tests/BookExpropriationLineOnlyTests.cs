using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using Reporting.Infrastructure.Rendering;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>The reports read แนวเวนคืน only (IsInExpropriationLine, ExpropriationLineRemark); the legacy IsExpropriated "ระวางเวนคืน" and its remark are never read.</summary>
public class BookExpropriationLineOnlyTests
{
    // Dapper refuses a result set with no columns at all, so every empty set carries one unused column.
    private static DataTable Empty() { var t = new DataTable(); t.Columns.Add("Unused", typeof(int)); return t; }

    private static FakeSqlConnectionFactory FactoryWith(params DataTable[] sets)
    {
        var ds = new DataSet();
        foreach (var t in sets) ds.Tables.Add(t);
        for (var i = sets.Length; i < 15; i++) ds.Tables.Add(Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);
        return f;
    }

    private static async Task<(LandSection Land, string Legal, FakeSqlConnectionFactory Factory)> LoadLandAsync(bool? inLine, string? lineRemark)
    {
        var id = Guid.NewGuid();
        var land = new DataTable();
        foreach (var (n, t) in new (string, Type)[]
                 {
                     ("LandDetailId", typeof(Guid)), ("IsInExpropriationLine", typeof(bool)), ("ExpropriationLineRemark", typeof(string)),
                     // columns an old query would have returned: the loader must ignore them
                     ("IsExpropriated", typeof(bool)), ("ExpropriationRemark", typeof(string))
                 })
            land.Columns.Add(n, t);
        land.Rows.Add(id, inLine is null ? DBNull.Value : inLine, (object?)lineRemark ?? DBNull.Value, true, "legacy remark");
        var factory = FactoryWith(land);

        var lands = await LandSectionLoader.LoadAllAsync(factory.GetOpenConnection(), Guid.NewGuid());
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ include 'partials/book-funcs' }}{{ multi_land = false }}{{ for ls in model.all_lands }}{{ include 'partials/book-legal-limits' }}{{ end }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = lands }] }, CancellationToken.None);
        return (lands.Single(), html, factory);
    }

    [Fact]
    public async Task A_land_whose_only_expropriation_flag_is_the_legacy_one_prints_no_in_the_line()
    {
        var (land, html, factory) = await LoadLandAsync(inLine: null, lineRemark: null);

        land.IsInExpropriationLine.Should().BeNull();
        land.ExpropriationRemark.Should().BeNull();
        html.Should().NotContain("อยู่ในแนวเวนคืน").And.NotContain("legacy remark");
        factory.Commands.Should().OnlyContain(c => !c.Contains("IsExpropriated") && !c.Contains("lad.ExpropriationRemark"));
    }

    [Fact]
    public async Task The_land_remark_comes_from_the_line_remark_only_and_an_explicit_in_the_line_prints()
    {
        var (land, html, _) = await LoadLandAsync(inLine: true, lineRemark: "ถนนสายใหม่");

        land.ExpropriationRemark.Should().Be("ถนนสายใหม่");
        html.Should().Contain("อยู่ในแนวเวนคืน").And.NotContain("ถนนสายใหม่").And.NotContain("legacy remark"); // the line remark lives in the Law & Regulation records
    }

    [Fact]
    public async Task The_condo_and_block_queries_read_the_line_columns_and_never_the_legacy_ones()
    {
        var condo = FactoryWith();
        (await CondoSectionLoader.LoadAsync(condo.GetOpenConnection(), Guid.NewGuid())).Should().BeNull();
        var block = FactoryWith();
        await BlockBookSectionLoader.LoadAsync(block.GetOpenConnection(), Guid.NewGuid(), d => d);

        condo.Commands.Should().ContainSingle().Which.Should().Contain("cad.IsInExpropriationLine").And.Contain("cad.ExpropriationLineRemark")
            .And.NotContain("IsExpropriated").And.NotContain("cad.ExpropriationRemark");
        block.Commands.Should().ContainSingle().Which.Should().Contain("pl.IsInExpropriationLine").And.Contain("pl.ExpropriationLineRemark")
            .And.NotContain("IsExpropriated").And.NotContain("pl.ExpropriationRemark");
    }

    [Theory]
    [InlineData(true, null, "อยู่ในแนวเวนคืน")]
    [InlineData(true, "  ถนนสายใหม่ ", "อยู่ในแนวเวนคืน (ถนนสายใหม่)")]
    [InlineData(false, "ไม่ใช้", "ไม่อยู่ในแนวเวนคืน")]
    [InlineData(null, "ไม่ใช้", null)]
    public void The_block_book_prints_the_line_answer_and_its_remark(bool? inLine, string? remark, string? expected)
        => BlockBookSectionLoader.ExpropriationText(inLine, remark).Should().Be(expected);

    [Fact]
    public async Task The_land_loader_keeps_the_accessibility_text_and_its_remark_apart()
    {
        var land = new DataTable();
        foreach (var (n, t) in new (string, Type)[] { ("LandDetailId", typeof(Guid)), ("LandAccessibilityType", typeof(string)), ("LandAccessibilityRemark", typeof(string)) })
            land.Columns.Add(n, t);
        land.Rows.Add(Guid.NewGuid(), "03", "ต้องถมทางเข้า");
        var prms = new DataTable();
        foreach (var n in new[] { "Group", "Code", "Description" }) prms.Columns.Add(n, typeof(string));
        prms.Rows.Add("LandAccessibility", "03", "ต้องมีการปรับปรุงสภาพทางเข้า-ออก");

        var lands = await LandSectionLoader.LoadAllAsync(FactoryWith(land, Empty(), prms).GetOpenConnection(), Guid.NewGuid());

        lands.Single().LandAccessibility.Should().Be("ต้องมีการปรับปรุงสภาพทางเข้า-ออก");
        lands.Single().LandAccessibilityRemark.Should().Be("ต้องถมทางเข้า");
    }
}

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>The cost-approach building table when the appraiser typed the Building Cost by hand (ด้วยตนเอง).</summary>
public class BuildingCostManualTemplateTests
{
    private static Task<string> RenderAsync(string setup) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ cost_blds = [{ building_number: 1, cost_rows: [] }]; cost_dev = []; " + setup + " }}{{ include 'partials/book-building-cost' }}",
            new AppraisalSummaryModel(), CancellationToken.None);

    [Fact]
    public async Task Typed_building_cost_prints_the_computed_total_then_the_appraisers_value_and_note()
    {
        var html = await RenderAsync("cost_total = 2000000; cost_computed = 19699000; cost_override = 2000000; cost_manual_remark = \"ที่มา: ประเมินเอง\"");

        html.Should().Contain("รวมมูลค่าสิ่งปลูกสร้าง (คำนวณ)").And.Contain("19,699,000.00")
            .And.Contain("ปรับโดยผู้ประเมิน").And.Contain("2,000,000.00")
            .And.Contain("หมายเหตุ: ที่มา: ประเมินเอง").And.Contain("pre-line");
        html.Should().NotContain("ตามสภาพปัจจุบัน");
    }

    [Fact]
    public async Task System_calculated_building_cost_prints_as_before()
    {
        var html = await RenderAsync("cost_total = 19699000; cost_override = null");

        html.Should().Contain("รวมมูลค่าสิ่งปลูกสร้าง ตามสภาพปัจจุบัน")
            .And.NotContain("ปรับโดยผู้ประเมิน").And.NotContain("หมายเหตุ:");
    }
}

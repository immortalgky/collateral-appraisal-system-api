using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>A factor value that is text (a long comma-separated list) must wrap; only figures keep the one-line .num cell, and the grid has a fixed layout so one long cell cannot squash its neighbours.</summary>
public class SaleGridWrapTests
{
    private const string LongList = "บ้านตัวอย่าง, แปลงหัวมุม, ใกล้โมสร, บ้านไม่หันหลังชนกับใคร, ติดถนนสองด้าน, น้ำไม่ท่วม";

    private static Task<string> RenderAsync(SaleGridSection sgr) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ sgr = model.sale_grid_sections[0] }}{{ include 'partials/section-salegrid' }}",
            new AppraisalSummaryModel { SaleGridSections = [sgr] }, CancellationToken.None);

    private static SaleGridSection Grid(params SaleGridRow[] rows) => new()
    {
        ComparableHeaders = [new MarketHeader { Title = "ตลาด 1" }],
        Chunks = [new TableChunk { Start = 0, Count = 1 }],
        Rows = rows
    };

    [Fact]
    public async Task A_text_factor_value_wraps_and_a_figure_row_keeps_nowrap()
    {
        var html = await RenderAsync(Grid(
            new SaleGridRow { Label = "ลักษณะ", SubjectValue = LongList, DashEmpty = true, Values = [LongList], Amounts = ["เท่ากัน"] },
            new SaleGridRow { Label = "ขนาดที่ดิน", SubjectValue = "100", DashEmpty = true, Values = ["1,200.50"], Amounts = ["ดีกว่า"] },
            new SaleGridRow { Label = "ตำแหน่งที่ตั้ง", DashEmpty = true, TextFactor = true, Values = [null], Amounts = ["เท่ากัน"] },
            new SaleGridRow { Label = "ระยะทาง", DashEmpty = true, Values = [null], Amounts = ["เท่ากัน"] },
            new SaleGridRow { Label = "ราคาเสนอขาย", Unit = "บาท", Values = ["3,500,000.00"] },
            new SaleGridRow { Label = "ถนน", SubjectValue = LongList, Values = ["5.00"], Amounts = ["10,000.00"] }));

        html.Should().Contain($"<td>{LongList}</td>");                       // the text cell has no class, so no nowrap
        html.Should().NotContain($"<td class=\"num\">{LongList}</td>");
        html.Should().Contain("<td>-</td>");                                 // an empty text factor follows the text alignment
        html.Should().Contain("<td class=\"num\">-</td>");                  // an empty numeric factor stays with the figures
        html.Should().Contain("<td class=\"num\">1,200.50</td>");           // a figure in a factor row still never breaks
        html.Should().Contain("<td class=\"num\" colspan=\"2\">3,500,000.00</td>"); // a price row too
        html.Should().MatchRegex($"<td>{LongList}</td>\\s*<td class=\"num\">5.00</td>");   // the remark in the adjustment band wraps; its percentage does not
        html.Should().Contain("table-layout:fixed");
    }

    [Theory]
    [InlineData("1,200.50", false)]
    [InlineData("-3.5", false)]
    [InlineData("ถนนลาดยาง", true)]
    [InlineData("A, B", true)]
    public void A_row_is_text_when_any_cell_is_not_a_figure(string value, bool text)
    {
        new SaleGridRow { Values = [null, value] }.ValuesAreText.Should().Be(text);
    }
}

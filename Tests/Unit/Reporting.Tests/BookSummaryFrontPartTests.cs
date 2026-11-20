using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf;
using Reporting.Application.Models;
using Reporting.Application.Services;
using Reporting.Infrastructure.PdfAssembly;
using Reporting.Infrastructure.Rendering;
using Shared.Configurations;

namespace Reporting.Tests;

/// <summary>The internal book's appraisal-summary form(s) are a numbered front part (II, III …) listed first in the สารบัญ; the body still starts at 1.</summary>
public class BookSummaryFrontPartTests
{
    private static Task<IReadOnlyList<RenderSegment>> SegmentsAsync(AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderAsync("{{ include 'appraisal-book' }}", model, CancellationToken.None);

    // The body flows of each type read their own section, so a bare section stands in for it.
    private static AppraisalSummaryModel Model(string body, bool external = false, string? reportTitle = null) =>
        new()
        {
            BodyType = body, IsExternal = external, ReportTitle = reportTitle, AppraisalBookNumber = "A-1",
            BlockSection = body == "block" ? new Reporting.Application.Models.Sections.BlockBookSection() : null,
            MachineSection = body == "machine" ? new Reporting.Application.Models.Sections.MachineSection() : null,
            CondoSection = body == "condo" ? new Reporting.Application.Models.Sections.CondoSection() : null
        };

    private static IEnumerable<BookPart> Parts(IEnumerable<RenderSegment> segments) => segments.OfType<HtmlSegment>().Select(s => s.Part!);

    [Theory]
    [InlineData("standard", "สรุปรายงานการประเมินราคาทรัพย์สิน")]
    [InlineData("condo", "สรุปรายงานการประเมินราคาทรัพย์สิน")]
    [InlineData("machine", "สรุปรายงานการประเมินราคาทรัพย์สิน")]
    [InlineData("block", "สรุปรายงานการประเมินราคาทรัพย์สิน")]
    [InlineData("construction", "สรุปรายงานการตรวจงานก่อสร้าง")]
    public async Task An_internal_book_has_the_summary_as_a_front_part_after_the_contents_and_lists_it_first(string body, string title)
    {
        var segments = await SegmentsAsync(Model(body));

        var parts = Parts(segments).Select(p => (p.Kind, p.Id)).ToList();
        var toc = parts.IndexOf((BookPartKind.Front, "toc"));
        var summary = parts.IndexOf((BookPartKind.Front, "summary"));
        var firstBody = parts.FindIndex(p => p.Kind == BookPartKind.Body);
        toc.Should().BeGreaterThanOrEqualTo(0);
        summary.Should().Be(toc + 1);
        (firstBody < 0 || summary < firstBody).Should().BeTrue();
        parts.Should().NotContain(p => p.Kind == BookPartKind.Raw && p.Id == "summary");

        var tocHtml = segments.OfType<HtmlSegment>().First(s => s.Part is { Kind: BookPartKind.Front, Id: "toc" }).Html;
        tocHtml.Should().Contain($"<span>{title}</span><i></i><span>[[PAGE:summary]]</span>");
        tocHtml.IndexOf("[[PAGE:summary]]", StringComparison.Ordinal).Should().BeLessThan(tocHtml.IndexOf("[[PAGE:s1]]", StringComparison.Ordinal) is var s1 and >= 0 ? s1 : int.MaxValue);
    }

    [Fact]
    public async Task The_standard_summary_line_uses_the_report_title_when_there_is_one()
    {
        var segments = await SegmentsAsync(Model("standard", reportTitle: "สรุปรายงานการประเมินราคาทรัพย์สิน (ฉบับแก้ไข)"));

        segments.OfType<HtmlSegment>().First(s => s.Part is { Id: "toc" }).Html.Should().Contain("<span>สรุปรายงานการประเมินราคาทรัพย์สิน (ฉบับแก้ไข)</span><i></i><span>[[PAGE:summary]]</span>");
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("construction")]
    public async Task An_external_book_has_no_summary_part_and_no_line(string body)
    {
        var segments = await SegmentsAsync(Model(body, external: true));

        Parts(segments).Should().NotContain(p => p.Id == "summary");
        segments.OfType<HtmlSegment>().Should().NotContain(s => s.Html.Contains("[[PAGE:summary]]"));
    }

    [Fact]
    public async Task A_body_type_without_a_summary_form_gets_neither_the_part_nor_the_line()
    {
        var segments = await SegmentsAsync(Model("lease"));

        Parts(segments).Should().NotContain(p => p.Id == "summary");
        segments.OfType<HtmlSegment>().Should().NotContain(s => s.Html.Contains("[[PAGE:summary]]"));
    }

    private sealed class PagesRenderer(string summaryHtml) : IPdfRenderer
    {
        public List<string> Htmls { get; } = [];

        public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken)
        {
            lock (Htmls) Htmls.Add(html);
            // the overlay: one page per numbered page; the summary part: 2 pages; everything else 1
            var pages = Regex.Matches(html, "class=\"ov").Count is var ov and > 0 ? ov : html == summaryHtml ? 2 : 1;
            using var doc = new PdfDocument();
            for (var i = 0; i < pages; i++) doc.AddPage();
            using var ms = new MemoryStream();
            doc.Save(ms);
            return Task.FromResult(ms.ToArray());
        }
    }

    [Fact]
    public async Task The_summary_pages_are_numbered_II_and_III_the_contents_says_so_and_the_body_still_starts_at_1()
    {
        var segments = await SegmentsAsync(Model("standard"));
        var renderer = new PagesRenderer(segments.OfType<HtmlSegment>().First(x => x.Part is { Id: "summary" }).Html);
        var assembler = new PdfSharpAssembler(renderer, null!, Options.Create(new FileStorageConfiguration()), NullLogger<PdfSharpAssembler>.Instance);

        await assembler.AssembleAsync(segments.ToList(), CancellationToken.None);

        var toc = renderer.Htmls.Last(h => h.Contains("สารบัญ"));
        toc.Should().Contain("<span>สรุปรายงานการประเมินราคาทรัพย์สิน</span><i></i><span>II</span>").And.NotContain("[[PAGE:");
        var overlay = renderer.Htmls.Single(h => h.Contains("class=\"ov"));
        overlay.Should().Contain("- I -").And.Contain("- II -").And.Contain("- III -").And.NotContain("- IV -").And.Contain("- 1 -");
    }
}

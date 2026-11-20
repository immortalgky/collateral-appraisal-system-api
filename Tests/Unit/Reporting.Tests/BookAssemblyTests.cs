using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Reporting.Application.Services;
using Reporting.Infrastructure.PdfAssembly;
using Reporting.Infrastructure.Rendering;
using Shared.Configurations;

namespace Reporting.Tests;

/// <summary>
/// The appraisal book is cut into PART markers; the assembler numbers the pages (front I, II…, body 1, 2… with attachments
/// counted), fills the contents tokens and asks for one overlay page per numbered page.
/// </summary>
public class BookAssemblyTests
{
    private static byte[] BlankPdf(int pages)
    {
        using var doc = new PdfDocument();
        for (var i = 0; i < pages; i++) doc.AddPage();
        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Renders "pages=N" markers as N blank pages; the overlay document is rendered as one page per .ov block.</summary>
    private sealed class FakeRenderer : IPdfRenderer
    {
        public List<string> Htmls { get; } = [];

        public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken)
        {
            lock (Htmls) Htmls.Add(html);
            var overlay = System.Text.RegularExpressions.Regex.Matches(html, "class=\"ov").Count;
            var m = System.Text.RegularExpressions.Regex.Match(html, @"pages=(\d+)");
            return Task.FromResult(BlankPdf(overlay > 0 ? overlay : m.Success ? int.Parse(m.Groups[1].Value) : 1));
        }
    }

    private sealed class FixedAttachment(string path) : IReportAttachmentSource
    {
        public Task<string?> GetFilePathAsync(Guid documentId, CancellationToken cancellationToken) => Task.FromResult<string?>(path);
    }

    private static PdfSharpAssembler Assembler(FakeRenderer renderer, string attachmentPath) =>
        new(renderer, new FixedAttachment(attachmentPath), Options.Create(new FileStorageConfiguration()),
            NullLogger<PdfSharpAssembler>.Instance);

    [Fact]
    public async Task Renderer_cuts_parts_and_keeps_a_slot_inside_its_part()
    {
        var html = "<html><head><style>x{}</style></head><body>" +
                   "<!-- PART: raw -->cover" +
                   "<!-- PART: front toc -->contents [[PAGE:s1]]" +
                   "<!-- PART: body s1 -->first<!-- SLOT: a -->after</body></html>";

        var segments = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderAsync(html, new { }, CancellationToken.None);

        var parts = segments.OfType<HtmlSegment>().Select(s => s.Part).ToList();
        parts.Select(p => (p!.Kind, p.Id)).Should().Equal(
            (BookPartKind.Raw, null), (BookPartKind.Front, "toc"), (BookPartKind.Body, "s1"), (BookPartKind.Body, "s1"));
        segments.OfType<AttachmentSlotSegment>().Should().ContainSingle();
        // the shell (head/styles) is carried to every part, but the text before the first marker is not a page
        segments.OfType<HtmlSegment>().Should().OnlyContain(s => s.Html.Contains("<style>"));
    }

    [Fact]
    public async Task Pages_are_numbered_roman_then_arabic_and_the_contents_gets_the_real_numbers()
    {
        var attachment = Path.GetTempFileName();
        await File.WriteAllBytesAsync(attachment, BlankPdf(3));
        try
        {
            var renderer = new FakeRenderer();
            var segments = new List<RenderSegment>
            {
                new HtmlSegment("<!-- book-report-no: 6700012 -->pages=2", new BookPart(BookPartKind.Raw, null)),
                new HtmlSegment("pages=1 [[PAGE:s1]] [[PAGE:s2]] [[PAGE:cond]] [[PAGE:attach]]", new BookPart(BookPartKind.Front, "toc")),
                new HtmlSegment("pages=1", new BookPart(BookPartKind.Front, "cond")),
                new HtmlSegment("pages=2", new BookPart(BookPartKind.Body, "s1")),
                new HtmlSegment("pages=1", new BookPart(BookPartKind.Body, "s2")),
                new HtmlSegment("pages=1", new BookPart(BookPartKind.Body, "attach")),
                new AttachmentSlotSegment("a", [Guid.NewGuid()]),
            };

            var pdf = await Assembler(renderer, attachment).AssembleAsync(segments, CancellationToken.None);

            using var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            doc.PageCount.Should().Be(2 + 1 + 1 + 2 + 1 + 1 + 3); // raw 2, front 2, body 4, attachment 3

            // contents re-rendered with the labels: s1 starts on 1, s2 on 3, cond on II, attach on 4 (after s1 1-2, s2 3)
            renderer.Htmls.Should().Contain(h => h.Contains("1 3 II 4"));
            // the overlay stamps numbered pages only: I, II, 1..4 — not the raw pages and not the attachment
            var overlay = renderer.Htmls.Single(h => h.Contains("class=\"ov"));
            overlay.Should().Contain("- I -").And.Contain("- II -").And.Contain("- 1 -").And.Contain("- 4 -")
                .And.NotContain("- 5 -").And.Contain("รายงานเลขที่ : 6700012");
        }
        finally
        {
            File.Delete(attachment);
        }
    }

    [Fact]
    public async Task A_captioned_slot_stamps_a_title_block_on_every_pdf_page_without_adding_a_page()
    {
        var attachment = Path.GetTempFileName();
        await File.WriteAllBytesAsync(attachment, BlankPdf(2));
        try
        {
            var html = "<html><body><!-- PART: body s1 -->x<!-- SLOT: a --><!-- CAPTION: แผนที่สังเขป --><!-- pdf-margin-y:24mm --></body></html>";
            var segments = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
                .RenderAsync(html, new { }, CancellationToken.None);
            segments.OfType<AttachmentSlotSegment>().Single().Caption.Should().Be("แผนที่สังเขป");

            var renderer = new FakeRenderer();
            var withIds = segments.Select(s => s is AttachmentSlotSegment slot ? slot with { DocumentIds = [Guid.NewGuid()] } : s).ToList();
            var pdf = await Assembler(renderer, attachment).AssembleAsync(withIds, CancellationToken.None);

            using var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            doc.PageCount.Should().Be(1 + 2); // body page + the 2 attached pages: no caption-only page
            // one title block (caption + report number) on every page of the captioned file
            var overlay = renderer.Htmls.Single(h => h.Contains("class=\"ov"));
            System.Text.RegularExpressions.Regex.Matches(overlay, "class=\"tb\"").Count.Should().Be(2);
            overlay.Should().Contain("<td class=\"v\">แผนที่สังเขป</td>");
        }
        finally
        {
            File.Delete(attachment);
        }
    }

    [Theory]
    [InlineData("BRIEF_MAP", 1, 0, false)]
    [InlineData("LAND_MAP", 0, 1, false)]      // a PDF land map is still a map
    [InlineData("PHOTO_SPOT", 3, 0, false)]
    [InlineData("REG_INDEX", 2, 0, true)]      // copy of a register: an attachment whatever its format
    [InlineData("TITLE_DEED_COPY", 0, 2, true)] // no rule for this code: PDFs only -> attachment
    [InlineData("TITLE_DEED_COPY", 1, 0, false)] // no rule: images -> appendix
    [InlineData(null, 2, 0, false)]            // Photos-tab topic
    public void Appendix_groups_split_by_type_code_then_by_format(string? code, int images, int pdfs, bool attachment) =>
        Reporting.Application.Providers.Sections.AppendixSectionLoader.IsAttachmentGroup(code, images, pdfs).Should().Be(attachment);

    [Fact]
    public void Roman_numerals()
    {
        BookChrome.Roman(1).Should().Be("I");
        BookChrome.Roman(4).Should().Be("IV");
        BookChrome.Roman(9).Should().Be("IX");
    }
}

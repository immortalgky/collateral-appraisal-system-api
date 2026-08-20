using System.Text.RegularExpressions;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Reporting.Application.Services;
using Reporting.Infrastructure.BrowserPool;

namespace Reporting.Infrastructure.Rendering;

/// <summary>
/// Renders an HTML string to A4 PDF bytes via a pooled headless Chromium instance.
///
/// Font loading strategy:
///   Write the HTML to a temp .html file alongside the Templates directory so that
///   relative @font-face url(...) paths in the CSS resolve correctly (Chromium can
///   only load local fonts via file:// URIs). The temp file is deleted after rendering.
///
///   If no TTF is present, Chromium falls back to the CSS font-stack which includes
///   system Thai fonts (Sarabun, Noto Sans Thai, Thonburi, etc.).
/// </summary>
internal sealed class PuppeteerPdfRenderer(
    IBrowserPool browserPool,
    ILogger<PuppeteerPdfRenderer> logger)
    : IPdfRenderer
{
    private static readonly string TemplatesRoot =
        Path.Combine(AppContext.BaseDirectory, "Templates");

    // Default page margin applied on all four sides (matches the historical behaviour).
    private const string DefaultMargin = "15mm";

    // A template can opt into narrower PDF page margins by emitting directive comments in its
    // ROOT document — pdf-margin-x for LEFT/RIGHT, pdf-margin-y for TOP/BOTTOM (see
    // appraisal-summary-*.html, which use 5mm on both axes). Each axis is independent: a template
    // that declares only pdf-margin-x keeps the 15mm default top/bottom, and every report that
    // declares neither keeps 15mm all round.
    //
    // The length token MUST be immediately followed by the HTML comment terminator "-->". This
    // ties the directive to a template-authored comment: rendered report data is HTML-encoded, so a
    // data field can never emit a literal "-->" (the '>' becomes "&gt;") — preventing arbitrary
    // document content (e.g. an appraiser remark containing "pdf-margin-x:5mm") from changing the
    // page margin. The match timeout is a belt-and-braces ReDoS guard on the caller-supplied HTML.
    private static readonly Regex HorizontalMarginDirective = new(
        @"pdf-margin-x:\s*(?<len>\d+(?:\.\d+)?(?:mm|cm|in|px))\s*-->",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex VerticalMarginDirective = new(
        @"pdf-margin-y:\s*(?<len>\d+(?:\.\d+)?(?:mm|cm|in|px))\s*-->",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static string ResolveHorizontalMargin(string html)
        => ResolveMargin(HorizontalMarginDirective, html);

    private static string ResolveVerticalMargin(string html)
        => ResolveMargin(VerticalMarginDirective, html);

    private static string ResolveMargin(Regex directive, string html)
    {
        var match = directive.Match(html);
        return match.Success ? match.Groups["len"].Value : DefaultMargin;
    }

    public async Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken)
    {
        // Write HTML to a temp file next to the Templates directory so @font-face
        // relative paths resolve correctly when navigated via file:// URI.
        var tempFile = Path.Combine(TemplatesRoot, $"_render_{Guid.NewGuid():N}.html");
        try
        {
            await File.WriteAllTextAsync(tempFile, html, cancellationToken);

            await using var leased = await browserPool.AcquirePageAsync(cancellationToken);
            var page = leased.Page;

            // Navigate via file URI so relative font paths in CSS resolve
            var fileUri = new Uri(tempFile).AbsoluteUri;
            await page.GoToAsync(fileUri, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle0],
                Timeout = 30_000
            });

            // Page margins can be narrowed per-report, per axis, via in-document directives
            // (summary forms use 5mm on both); everything else keeps the 15mm default.
            var horizontalMargin = ResolveHorizontalMargin(html);
            var verticalMargin = ResolveVerticalMargin(html);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                // Honour CSS @page size when a template declares it (e.g. landscape pages);
                // templates without an @page rule keep the A4 format above.
                PreferCSSPageSize = true,
                MarginOptions = new MarginOptions
                {
                    Top = verticalMargin,
                    Bottom = verticalMargin,
                    Left = horizontalMargin,
                    Right = horizontalMargin
                }
            });

            logger.LogDebug("PDF rendered: {Bytes} bytes", pdfBytes.Length);
            return pdfBytes;
        }
        finally
        {
            // Clean up temp file
            try { File.Delete(tempFile); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete temp render file {File}", tempFile);
            }
        }
    }
}

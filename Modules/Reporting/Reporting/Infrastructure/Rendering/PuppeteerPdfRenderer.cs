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

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "15mm",
                    Bottom = "15mm",
                    Left = "15mm",
                    Right = "15mm"
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

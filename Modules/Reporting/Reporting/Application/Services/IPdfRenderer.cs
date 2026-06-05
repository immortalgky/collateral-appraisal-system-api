namespace Reporting.Application.Services;

/// <summary>
/// Converts an HTML string to PDF bytes via headless Chromium.
/// Implemented by <see cref="Reporting.Infrastructure.Rendering.PuppeteerPdfRenderer"/>.
/// </summary>
public interface IPdfRenderer
{
    /// <summary>
    /// Renders the HTML to an A4 PDF and returns the raw bytes.
    /// The caller is responsible for disposing / cleaning up any temp files.
    /// </summary>
    Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken);
}

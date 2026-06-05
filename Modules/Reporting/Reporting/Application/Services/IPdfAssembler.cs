namespace Reporting.Application.Services;

/// <summary>
/// Assembles ordered segments (rendered HTML PDFs + uploaded attachment PDFs)
/// into a single merged PDF document.
/// Implemented by <see cref="Reporting.Infrastructure.PdfAssembly.PdfSharpAssembler"/>.
/// </summary>
public interface IPdfAssembler
{
    /// <summary>
    /// Takes the ordered segment list (already rendered or resolved to file paths),
    /// merges all pages, and returns the final PDF bytes.
    /// </summary>
    Task<byte[]> AssembleAsync(
        IReadOnlyList<RenderSegment> segments,
        CancellationToken cancellationToken);
}

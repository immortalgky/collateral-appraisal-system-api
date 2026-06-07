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

    /// <summary>
    /// Concatenates already-rendered PDFs (e.g. the child forms of a composite report) into
    /// one document, preserving order. Returns the single input unchanged when there is exactly
    /// one; a valid one-page blank PDF when the list is empty.
    /// </summary>
    Task<byte[]> MergeAsync(
        IReadOnlyList<byte[]> pdfs,
        CancellationToken cancellationToken);
}

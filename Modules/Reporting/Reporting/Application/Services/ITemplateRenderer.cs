namespace Reporting.Application.Services;

/// <summary>
/// Renders a Scriban HTML template against a model, splitting at
/// <c>&lt;!-- SLOT: name --&gt;</c> markers into ordered segments.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Returns an ordered list of segments. Each segment is either:
    /// <list type="bullet">
    ///   <item><see cref="HtmlSegment"/>: a rendered HTML string (one page-group)</item>
    ///   <item><see cref="AttachmentSlotSegment"/>: a named slot whose document IDs
    ///     are sourced from the model's AttachmentsBySlot dictionary.</item>
    /// </list>
    /// </summary>
    Task<IReadOnlyList<RenderSegment>> RenderAsync(
        string templateHtml,
        object model,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renders a Scriban HTML template against a model and returns the full rendered HTML
    /// string as-is, without splitting on <c>&lt;!-- SLOT: name --&gt;</c> markers. Used by
    /// the browser-preview (HTML) report path, which has no PDF attachment merge step.
    /// </summary>
    Task<string> RenderRawAsync(
        string templateHtml,
        object model,
        CancellationToken cancellationToken);
}

/// <summary>Base discriminated union for rendered segments.</summary>
public abstract record RenderSegment;

/// <summary>A rendered HTML fragment that maps to one or more PDF pages.</summary>
public sealed record HtmlSegment(string Html) : RenderSegment;

/// <summary>
/// A named attachment slot. The PDF assembler inserts uploaded PDFs
/// (by DocumentId) at this position in the final merged document.
/// </summary>
public sealed record AttachmentSlotSegment(
    string SlotName,
    IReadOnlyList<Guid> DocumentIds) : RenderSegment;

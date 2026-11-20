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

/// <summary>How the pages of a book part are treated by the assembler (see <see cref="BookPart"/>).</summary>
public enum BookPartKind
{
    /// <summary>Pasted pages (cover, the bank's summary form): no running header/footer, no page number, not counted.</summary>
    Raw,
    /// <summary>Front matter (สารบัญ, เงื่อนไข): running header/footer, numbered I, II, III…</summary>
    Front,
    /// <summary>Body: running header/footer, numbered 1, 2, 3… from the first body part on.</summary>
    Body
}

/// <summary>
/// A template opts a region into book pagination with a <c>&lt;!-- PART: raw|front|body [id] --&gt;</c> marker.
/// <see cref="Id"/> names the part so a contents line can ask for its page with a <c>[[PAGE:id]]</c> token.
/// </summary>
public sealed record BookPart(BookPartKind Kind, string? Id);

/// <summary>
/// A rendered HTML fragment that maps to one or more PDF pages. <see cref="Part"/> is set only for templates
/// that use PART markers (the appraisal book); every other report leaves it null and is assembled as before.
/// </summary>
public sealed record HtmlSegment(string Html, BookPart? Part = null) : RenderSegment;

/// <summary>
/// A named attachment slot. The PDF assembler inserts uploaded PDFs
/// (by DocumentId) at this position in the final merged document. <see cref="Caption"/> (book only; already
/// HTML-encoded) is the title block stamped on EVERY page of EVERY PDF of a captioned slot; each of those pages is
/// redrawn smaller to leave room for it (an unreadable file falls back to the plain merge, without the shrink).
/// </summary>
public sealed record AttachmentSlotSegment(
    string SlotName,
    IReadOnlyList<Guid> DocumentIds,
    string? Caption = null) : RenderSegment;

namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the appendix (ภาคผนวก) — FSD §2.1.2.12+.
///
/// Gallery entries are grouped by appendix type. Within each group, image-type entries
/// render as HTML image pages and PDF-type entries are merged — via a per-group SLOT
/// marker (<see cref="AppendixGroup.SlotName"/>) — right under that group's heading,
/// so a PDF uploaded under "Land Map" appears under the Land Map section.
/// The section is absent when no appendix rows exist for the appraisal.
/// </summary>
public sealed class AppendixSection
{
    /// <summary>
    /// One entry per appendix type that contains at least one image OR one PDF.
    /// Ordered by AppraisalAppendices.SortOrder.
    /// </summary>
    public IReadOnlyList<AppendixGroup> Groups { get; init; } = [];
}

/// <summary>One appendix type group, e.g. "แผนที่สังเขป".</summary>
public sealed class AppendixGroup
{
    /// <summary>
    /// Thai display name resolved from the Code→Thai map, falling back to
    /// AppendixTypes.Name when the code is not in the map.
    /// Null when neither source is available.
    /// </summary>
    public string? TypeNameThai { get; init; }

    /// <summary>
    /// Number of columns for the CSS image grid. Source: AppraisalAppendices.LayoutColumns.
    /// Defaults to 2 when the DB value is null or zero.
    /// </summary>
    public int LayoutColumns { get; init; } = 2;

    /// <summary>
    /// Images belonging to this group, ordered by AppendixDocuments.DisplaySequence.
    /// May be empty when the group has only PDF entries.
    /// </summary>
    public IReadOnlyList<AppendixImage> Images { get; init; } = [];

    /// <summary>
    /// Unique slot name for this group (e.g. "appendix-0"). The partial emits a
    /// <c>&lt;!-- SLOT: {SlotName} --&gt;</c> marker after the group's images so this
    /// group's PDF entries — keyed by the same name in the model's AttachmentsBySlot —
    /// are merged immediately under the group, not at the end of the document.
    /// </summary>
    public string SlotName { get; init; } = string.Empty;
}

/// <summary>One image entry inside an appendix group.</summary>
public sealed class AppendixImage
{
    /// <summary>
    /// Image src attribute value — "file://{FilePath}".
    /// Null when FilePath is empty or null in the gallery row.
    /// </summary>
    public string? ImgSrc { get; init; }

    /// <summary>Caption text from AppraisalGallery.Caption. Null when absent.</summary>
    public string? Caption { get; init; }
}

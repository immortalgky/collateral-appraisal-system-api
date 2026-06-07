namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the appendix (ภาคผนวก) — FSD §2.1.2.12+.
///
/// Image-type gallery entries are grouped by appendix type and rendered as HTML image pages.
/// PDF-type entries are collected as DocumentIds and merged via the existing SLOT mechanism.
/// The section is absent when no appendix rows exist for the appraisal.
/// </summary>
public sealed class AppendixSection
{
    /// <summary>
    /// One entry per appendix type that contains at least one image.
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
    /// </summary>
    public IReadOnlyList<AppendixImage> Images { get; init; } = [];
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

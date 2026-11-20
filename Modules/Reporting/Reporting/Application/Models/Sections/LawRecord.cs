namespace Reporting.Application.Models.Sections;

/// <summary>One Law &amp; Regulation record of the appraisal (appraisal.LawAndRegulations) with its images.</summary>
public sealed class LawRecord
{
    /// <summary>Thai label of the HeaderCode (parameter group LawRegulationHeader); the raw code when it has no label.</summary>
    public string Topic { get; init; } = string.Empty;

    public string? Remark { get; init; }

    /// <summary>Images in DisplaySequence order; Caption is the image Title, else its Description.</summary>
    public IReadOnlyList<AppendixImage> Images { get; init; } = [];
}

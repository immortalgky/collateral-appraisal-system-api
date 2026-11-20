namespace Reporting.Application.Models;

/// <summary>One part of the property on the external summary page: a bold title and its text lines (the first runs on after the title).</summary>
public sealed class ExternalPropertyBlock
{
    public string Title { get; init; } = "";
    public IReadOnlyList<string> Lines { get; init; } = [];
}

/// <summary>One condo unit's owner and obligation texts on the external book ("ห้อง 101", the mortgage line and the others line, "ไม่มี" when none).</summary>
public sealed class ExternalObligationItem
{
    public string Label { get; init; } = "";
    /// <summary>The room's owner as the letter prints it ("ไม่สามารถตรวจสอบได้" when not verified); null when none.</summary>
    public string? Owner { get; init; }
    public string? Mortgage { get; init; }
    public string? Others { get; init; }
}

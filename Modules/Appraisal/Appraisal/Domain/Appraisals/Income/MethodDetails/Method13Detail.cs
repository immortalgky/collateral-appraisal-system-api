using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>
/// Method 13 — Proportional (% of another section / category / assumption).
/// The refTarget identifies the source by kind and database or client-side ID.
/// </summary>
public sealed record Method13Detail
{
    [JsonPropertyName("proportionPct")]
    public decimal ProportionPct { get; init; }

    [JsonPropertyName("refTarget")]
    public RefTarget RefTarget { get; init; } = new();

    [JsonPropertyName("startIn")]
    public int StartIn { get; init; }
}

public sealed record RefTarget
{
    /// <summary>"section" | "category" | "assumption"</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>Client-side transient ID (used before first save).</summary>
    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    /// <summary>Database ID (available after first save).</summary>
    [JsonPropertyName("dbId")]
    public string? DbId { get; init; }
}

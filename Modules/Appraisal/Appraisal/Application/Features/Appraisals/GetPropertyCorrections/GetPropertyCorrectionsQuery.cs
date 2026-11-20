namespace Appraisal.Application.Features.Appraisals.GetPropertyCorrections;

/// <summary>
/// Correction history for an appraisal, newest first. Optionally narrowed to one property.
/// </summary>
public record GetPropertyCorrectionsQuery(Guid AppraisalId, Guid? PropertyId)
    : IQuery<GetPropertyCorrectionsResult>;

public record GetPropertyCorrectionsResult(IReadOnlyList<PropertyCorrectionEntry> Corrections);

/// <summary>
/// <see cref="AppraisalPropertyId"/> is null for document entries (PropertyType "DOCUMENT") and project entries
/// (PropertyType "PROJECT…"). <see cref="TargetId"/> is the model or tower of a project model / tower entry.
/// </summary>
public record PropertyCorrectionEntry(
    Guid Id,
    Guid? AppraisalPropertyId,
    string PropertyType,
    Guid? TargetId,
    string Reason,
    string ChangedBy,
    DateTime ChangedAt,
    IReadOnlyList<PropertyCorrectionChange> Changes);

/// <summary>
/// One field change. The stored JSON diff is expanded server-side so the client renders a flat
/// table instead of parsing JSON.
/// </summary>
public record PropertyCorrectionChange(string Field, string? From, string? To);

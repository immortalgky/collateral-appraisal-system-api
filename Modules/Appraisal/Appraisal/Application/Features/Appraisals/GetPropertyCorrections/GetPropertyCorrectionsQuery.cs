namespace Appraisal.Application.Features.Appraisals.GetPropertyCorrections;

/// <summary>
/// Correction history for an appraisal, newest first. Optionally narrowed to one property.
/// </summary>
public record GetPropertyCorrectionsQuery(Guid AppraisalId, Guid? PropertyId)
    : IQuery<GetPropertyCorrectionsResult>;

public record GetPropertyCorrectionsResult(IReadOnlyList<PropertyCorrectionEntry> Corrections);

public record PropertyCorrectionEntry(
    Guid Id,
    Guid AppraisalPropertyId,
    string PropertyType,
    string Reason,
    string ChangedBy,
    DateTime ChangedAt,
    IReadOnlyList<PropertyCorrectionChange> Changes);

/// <summary>
/// One field change. The stored JSON diff is expanded server-side so the client renders a flat
/// table instead of parsing JSON.
/// </summary>
public record PropertyCorrectionChange(string Field, string? From, string? To);

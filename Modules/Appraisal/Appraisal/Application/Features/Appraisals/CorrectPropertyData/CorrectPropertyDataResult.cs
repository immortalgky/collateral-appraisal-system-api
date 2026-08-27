namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>
/// Outcome of a correction. <see cref="ChangedFields"/> is the same JSON diff that was written to
/// the audit trail, returned so the caller can show exactly what was recorded.
/// </summary>
public record CorrectPropertyDataResult(
    Guid AppraisalId,
    Guid PropertyId,
    int ChangedFieldCount,
    string ChangedFields);

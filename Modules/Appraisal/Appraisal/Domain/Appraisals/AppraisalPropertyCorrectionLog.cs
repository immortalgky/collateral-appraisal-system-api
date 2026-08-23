namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Append-only record of one admin data correction applied to a closed appraisal's property.
///
/// Deliberately NOT an <see cref="Entity{TId}"/>: audit rows are never updated, so the
/// CreatedBy/UpdatedBy machinery of AuditableEntityInterceptor would only add noise. The actor and
/// timestamp are captured explicitly instead, and the row is written by
/// AppraisalPropertyCorrectionAuditLogWriter inside the same transaction as the correction.
///
/// Mirrors collateral.CollateralMasterAuditLogs so both admin-override trails read the same way.
/// </summary>
public class AppraisalPropertyCorrectionLog
{
    public Guid Id { get; private set; }
    public Guid AppraisalId { get; private set; }
    public Guid AppraisalPropertyId { get; private set; }

    /// <summary>Property type code (L / LB / U / MAC / …), denormalised for the history grid.</summary>
    public string PropertyType { get; private set; } = null!;

    /// <summary>
    /// JSON object keyed by dotted field path, each value <c>{ from, to }</c>.
    /// See <see cref="CorrectionDiff"/> for the key format.
    /// </summary>
    public string ChangedFields { get; private set; } = null!;

    public string Reason { get; private set; } = null!;
    public string ChangedBy { get; private set; } = null!;
    public DateTime ChangedAt { get; private set; }

    private AppraisalPropertyCorrectionLog()
    {
        // For EF Core
    }

    public AppraisalPropertyCorrectionLog(
        Guid appraisalId,
        Guid appraisalPropertyId,
        string propertyType,
        string changedFields,
        string reason,
        string changedBy,
        DateTime changedAt)
    {
        Id = Guid.CreateVersion7();
        AppraisalId = appraisalId;
        AppraisalPropertyId = appraisalPropertyId;
        PropertyType = propertyType;
        ChangedFields = changedFields;
        Reason = reason;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }
}

namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Append-only record of one admin data correction on a closed appraisal — the single history the data
/// correction page shows. Two kinds of row share it:
/// - property corrections (AppraisalPropertyId set), written by CorrectPropertyData;
/// - document-level entries (AppraisalPropertyId null, PropertyType <see cref="DocumentEntryType"/>),
///   written by CorrectAppraisalDocuments and by a reasoned summary regeneration. Joins and reports that
///   assume a property must allow for these.
///
/// Deliberately NOT an <see cref="Entity{TId}"/>: audit rows are never updated, so the
/// CreatedBy/UpdatedBy machinery of AuditableEntityInterceptor would only add noise. The actor and
/// timestamp are captured explicitly instead, and each row is written in the same transaction as the
/// change it describes.
///
/// Mirrors collateral.CollateralMasterAuditLogs so both admin-override trails read the same way.
/// </summary>
public class AppraisalPropertyCorrectionLog
{
    public Guid Id { get; private set; }
    public Guid AppraisalId { get; private set; }

    /// <summary>Null for entries that are not about one property (document corrections, summary regeneration).</summary>
    public Guid? AppraisalPropertyId { get; private set; }

    /// <summary>
    /// Property type code (L / LB / U / MAC / …), denormalised for the history grid.
    /// <see cref="DocumentEntryType"/> for document-level entries.
    /// </summary>
    public string PropertyType { get; private set; } = null!;

    /// <summary>
    /// JSON object keyed by dotted field path, each value <c>{ from, to }</c>:
    /// <c>Land.OwnerName</c>, or <c>Land.Titles[#1234].Rai</c> / <c>Building.DepreciationDetails[1].Periods…</c>
    /// for child rows (a row added or removed is one entry whose from/to is a one-line summary of the row).
    /// Rows written before the correction editor moved onto the real forms used
    /// <c>Land.Title[{titleId}].Field</c> for titles.
    /// </summary>
    public string ChangedFields { get; private set; } = null!;

    public string Reason { get; private set; } = null!;
    public string ChangedBy { get; private set; } = null!;
    public DateTime ChangedAt { get; private set; }

    public const string DocumentEntryType = "DOCUMENT";

    private AppraisalPropertyCorrectionLog()
    {
        // For EF Core
    }

    public AppraisalPropertyCorrectionLog(
        Guid appraisalId,
        Guid? appraisalPropertyId,
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

    /// <summary>
    /// Entry for a change to the appraisal's valuation documents rather than to a property. The keys of
    /// <paramref name="changedFields"/> are document type codes (or "AppraisalSummary" for a regeneration).
    /// </summary>
    public static AppraisalPropertyCorrectionLog ForDocuments(
        Guid appraisalId,
        string changedFields,
        string reason,
        string changedBy,
        DateTime changedAt) =>
        new(appraisalId, null, DocumentEntryType, changedFields, reason, changedBy, changedAt);
}

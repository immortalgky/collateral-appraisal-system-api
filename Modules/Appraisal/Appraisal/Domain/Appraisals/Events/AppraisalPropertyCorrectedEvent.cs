namespace Appraisal.Domain.Appraisals.Events;

/// <summary>
/// Raised when an admin corrects descriptive property data on a closed appraisal.
/// <paramref name="ChangedFields"/> is a JSON object keyed by dotted field path
/// ("Land.OwnerName", "Land.Title[{titleId}].TitleNumber"), each value being
/// <c>{ from, to }</c>.
///
/// Consumed by AppraisalPropertyCorrectionAuditLogWriter, which persists the audit row inside the
/// same transaction as the correction itself.
/// </summary>
public record AppraisalPropertyCorrectedEvent(
    Guid AppraisalId,
    Guid PropertyId,
    string PropertyType,
    string ChangedFields,
    string Reason,
    string By) : IDomainEvent;

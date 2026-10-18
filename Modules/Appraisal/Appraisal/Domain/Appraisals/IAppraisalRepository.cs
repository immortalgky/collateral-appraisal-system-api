namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Repository interface for Appraisal aggregate
/// </summary>
public interface IAppraisalRepository : IRepository<Appraisal, Guid>
{
    /// <summary>
    /// Get appraisal by ID with all properties loaded
    /// </summary>
    Task<Appraisal?> GetByIdWithPropertiesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get appraisal by ID with all related data (properties, groups, assignments)
    /// </summary>
    Task<Appraisal?> GetByIdWithAllDataAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get appraisal by appraisal number
    /// </summary>
    Task<Appraisal?> GetByAppraisalNumberAsync(string appraisalNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get appraisals by request ID
    /// </summary>
    Task<Appraisal?> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if appraisal exists for request
    /// </summary>
    Task<bool> ExistsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all appraisals
    /// </summary>
    Task<IEnumerable<Appraisal>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns lightweight display summaries for the given appraisal IDs.
    /// Used by CreateQuotationCommandHandler to populate QuotationRequestItem rows
    /// (PropertyType, PropertyLocation, AppraisalNumber, EstimatedValue, RequestId).
    /// </summary>
    Task<IReadOnlyList<AppraisalSummary>> GetSummariesAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight projection used when populating QuotationRequestItem display rows
/// for quotations created via the standalone POST /quotations endpoint.
/// </summary>
public sealed record AppraisalSummary(
    Guid AppraisalId,
    string? AppraisalNumber,
    /// <summary>CollateralType code from request.RequestTitles (e.g. "L", "LB", "U").</summary>
    string? PropertyType,
    /// <summary>Province + District from the first LandAppraisalDetail row, or null if none.</summary>
    string? PropertyLocation,
    /// <summary>Null — no estimated value is stored at the Appraisal level; callers pass null to AddItem.</summary>
    decimal? EstimatedValue,
    /// <summary>The parent request ID — used for RM resolution.</summary>
    Guid? RequestId);
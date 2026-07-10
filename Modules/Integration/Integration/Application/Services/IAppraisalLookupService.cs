namespace Integration.Application.Services;

public record AppraisalKeys(string? AppraisalNumber, string? ExternalCaseKey, string? ExternalSystem);

/// <summary>
/// A prior appraisal resolved from its external number: the in-system Id plus its current status
/// string (e.g. "Completed"), so the Integration boundary can both reference it and gate on status.
/// </summary>
public record PriorAppraisalRef(Guid Id, string? Status);

public interface IAppraisalLookupService
{
    Task<AppraisalKeys?> GetKeysAsync(Guid appraisalId, CancellationToken ct = default);
    Task<AppraisalKeys?> GetKeysByRequestIdAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Resolves an external-supplied appraisal number (a.k.a. SurveyNo) to the in-system appraisal
    /// Id and its current status. Used at the Integration boundary so external callers can reference
    /// a prior appraisal by its number instead of our internal GUID, and so the create-request
    /// handler can reject early when that prior appraisal is not yet Completed. Returns null when no
    /// non-deleted appraisal carries that number. AppraisalNumber has a filtered unique index, so at
    /// most one row matches.
    /// </summary>
    Task<PriorAppraisalRef?> ResolvePriorAppraisalByNumberAsync(string appraisalNumber, CancellationToken ct = default);
}

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
    
    /// <summary>
    /// Reads the committed PMA data for a single AppraisalProperty (land titles or condo fields) at
    /// delivery time, for mapping to the external LOS update payload. Returns null if the
    /// appraisal/property no longer exists.
    /// </summary>
    Task<PmaUpdateData?> GetPmaUpdateDataAsync(Guid appraisalId, Guid propertyId, CancellationToken ct = default);
}

/// <summary>
/// Committed PMA data for one AppraisalProperty, read directly from the appraisal/request schemas
/// (Dapper — no cross-module EF dependency). Exactly one of <see cref="LandTitles"/> (non-empty) or
/// <see cref="Condo"/> (non-null) is populated, depending on the property's type.
/// </summary>
// ExternalSystem is the owning Request's ExternalSystem (e.g. "LOS") — the dynamic routing key
// for the PMA push webhook subscription. Null/empty means an internal appraisal with nothing to sync.
public sealed record PmaUpdateData(
    string? AppraisalNumber,
    string? ExternalSystem,
    string? LoanApplicationNo,
    string? PropertyType,
    decimal? SellingPrice,
    decimal? ForcedSalePrice,
    decimal? BuildingInsurancePrice,
    IReadOnlyList<PmaLandTitleData> LandTitles,
    PmaCondoData? Condo);

/// <summary>
/// One land title deed row. Column order in <see cref="AppraisalLookupService"/>'s SQL SELECT must
/// mirror this record's constructor parameter order exactly (Dapper binds positional records by
/// position, not just by name).
/// </summary>
public sealed record PmaLandTitleData(
    string? TitleNumber,
    string? BookNumber,
    string? PageNumber,
    string? LandParcelNumber,
    string? SurveyNumber,
    string? Rawang,
    decimal? Rai,
    decimal? Ngan,
    decimal? SquareWa,
    string? SubDistrict,
    string? District,
    string? Province);

/// <summary>
/// Condo on-screen PMA fields. Column order in the SQL SELECT must mirror this record's
/// constructor parameter order exactly (see <see cref="PmaLandTitleData"/> remarks).
/// </summary>
public sealed record PmaCondoData(
    string? BuiltOnTitleNumber,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    string? FloorNumber,
    string? BuildingNumber,
    string? CondoName,
    string? SubDistrict,
    string? District,
    string? Province);

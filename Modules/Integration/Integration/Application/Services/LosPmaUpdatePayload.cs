namespace Integration.Application.Services;

/// <summary>
/// External LOS update payload shape (typed DTO, not a config template — per the "external mapping
/// lives in Integration" convention). Property names use LOS's exact JSON field names via
/// System.Text.Json's camelCase policy (already applied by WebhookService's envelope serializer),
/// with two spec typos intentionally FIXED on our side: "buildingInsurance" (not
/// "buldingInsurance") and "surveyNo" (not "surveryNo"). "titleID" is DROPPED — not sent.
/// "documentList" is intentionally omitted entirely.
/// </summary>
public sealed record LosPmaUpdatePayload(
    string CasReportNo,
    string? LoanApplicationNo,
    string Action,
    LosPmaDetails PmaDetails);

/// <summary>
/// <see cref="Collateral"/> is set by <see cref="LosPmaPayloadMapper"/> to a
/// <see cref="LosLandCollateral"/> (one payload per title) or a <see cref="LosCondoCollateral"/>
/// (one payload for the whole property) — never both, so each JSON payload only ever carries the
/// fields for its own property type (no cross-type null leakage).
/// </summary>
public sealed record LosPmaDetails(
    LosPmaPrices Pma,
    object Collateral);

public sealed record LosPmaPrices(
    decimal? SellingPrice,
    decimal? ForceSellingPrice,
    decimal? BuildingInsurance);

/// <summary>One payload per land title; prices in <see cref="LosPmaPrices"/> are repeated per title.</summary>
public sealed record LosLandCollateral(
    string? Rawang,
    string? LandNo,
    string? SurveyNo,
    string? TitleDeedNo,
    string? BookNo,
    string? PageNo,
    decimal? Rai,
    decimal? Ngan,
    decimal? Wa,
    string? SubDistrict,
    string? District,
    string? Province);

/// <summary>
/// LOS condo collateral fields, per the LOS-provided spec, PLUS the title deed + address sent the
/// same way as the land collateral (<c>titleDeedNo</c> ← builtOnTitleNumber, and subDistrict/
/// district/province). The spec's "buildigNo" typo is fixed to "buildingNo" (consistent with the land
/// typo-fixes; user notifies LOS). <c>usageArea</c>/<c>owner</c> are not captured in the PMA condo
/// data yet — sent null.
/// </summary>
public sealed record LosCondoCollateral(
    string? TitleDeedNo,
    string? RoomNo,
    decimal? FloorNo,
    string? BuildingNo,
    string? CondoName,
    string? CondoRegNo,
    decimal? UsageArea,
    string? Owner,
    string? SubDistrict,
    string? District,
    string? Province);

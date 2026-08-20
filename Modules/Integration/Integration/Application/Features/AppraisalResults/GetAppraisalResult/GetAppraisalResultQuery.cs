using Shared.CQRS;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

// Optional unit selector (PlotNumber for Land/Building block, RoomNumber+FloorNumber for Condo block)
// is used only when the appraisal is a block/project; ignored for normal appraisals.
public record GetAppraisalResultByNumberQuery(
    string AppraisalNumber,
    string? PlotNumber = null,
    string? RoomNumber = null,
    string? FloorNumber = null)
    : IQuery<GetAppraisalResultResponse?>;

public record GetAppraisalResultsByCaseKeyQuery(
    string ExternalCaseKey,
    string? PlotNumber = null,
    string? RoomNumber = null,
    string? FloorNumber = null)
    : IQuery<IReadOnlyList<GetAppraisalResultResponse>>;

public record GetAppraisalResultResponse(
    string AppraisalNumber,
    string? Status,
    string? AppraisalPurpose,
    decimal? AppraisalFee,
    string? AppraisalSource,
    string? ValuerName,
    string? ValuerCode,
    string? ValuationDate,
    string? AppraisalDate,
    decimal? TotalAppraisalValue,
    decimal? ForceSalePrice,
    decimal? FireInsurance,
    List<AppraisalResultGroup> Groups,
    List<AppraisalResultDocument> Documents);

public record AppraisalResultGroup(
    decimal? AppraisalValue,
    string? AppraisalMethod,
    decimal? LandValue,
    decimal? BuildingValue,
    decimal? UnitPrice,
    List<AppraisalResultCollateral> Collaterals);

public record AppraisalResultCollateral(
    string? CollateralType,
    // Land / LandAndBuilding
    string? TitleNo,
    string? LandNo,
    string? Rawang,
    string? SurveyNo,
    string? BookNo,
    string? PageNo,
    decimal? Rai,
    decimal? Ngan,
    decimal? Wa,
    // Building
    string? HouseNo,
    string? BuildingType,
    int? BuildingAge,
    decimal? TotalFloor,
    decimal? ConstructionPct,
    // Condo
    string? RoomNo,
    string? FloorNo,
    string? BuildingNo,
    decimal? AreaUtilize,
    // Leasehold
    string? ContractNo,
    string? LesseeName,
    string? LessorName,
    // All types
    string? Province,
    string? District,
    string? SubDistrict,
    string? LandOffice,
    // Vehicle/Vessel/Machinery identity
    string? VehicleRegistrationNo,
    string? VehicleBrand,
    string? VehicleModel,
    string? VesselRegistrationNo,
    string? VesselName,
    string? VesselType,
    string? MachineName,
    string? MachineBrand,
    string? MachineModel,
    string? MachineSerialNo);

public record AppraisalResultDocument(string? DocumentType, string? DocumentPath);

// ── Legacy-shaped variant (AS400 consumer): flat { ResultCode, ResultValue } envelope for ONE
// collateral, selected via ApplicationNo (AppraisalNumber) + Filter1/Filter2. AssetTypeId is
// currently ignored. See GetLegacyAppraisalResultQueryHandler for the selection/mapping logic.
public record GetLegacyAppraisalResultQuery(
    string ApplicationNo,
    int AssetTypeId,
    string? Filter1,
    string? Filter2)
    : IQuery<LegacyAppraisalResultEnvelope>;

public record LegacyAppraisalResultEnvelope(int ResultCode, LegacyAppraisalResult ResultValue);

// All fields default to the legacy "empty" representation ("" / 0 / 0.0) rather than null, so a
// not-found/error result can be emitted as `new LegacyAppraisalResult()`.
public record LegacyAppraisalResult(
    string ErrorMessage = "",
    string LandNo = "",
    decimal Rai = 0m,
    decimal Ngan = 0m,
    decimal Wah = 0m,
    string Rawang = "",
    string SurveyNo = "",
    decimal AreaUtilize = 0m,
    string BuildingRegisterNo = "",
    string BookNo = "",
    string PageNo = "",
    string InternalValuerCode = "",
    string InternalValuerName = "",
    decimal InternalValuation = 0m,
    string InternalValuationDate = "",
    string ExternalValuerCode = "",
    string ExternalValuerName = "",
    decimal ExternalValuation = 0m,
    string ExternalValuationDate = "",
    string BuildingDetails = "",
    string TitleNo = "",
    string BuildingNo = "",
    int BuildingAge = 0,
    string HouseNo = "",
    string RoomNo = "",
    string FloorNo = "",
    string FloorNumber = "",
    string Province = "",
    string District = "",
    string SubDistrict = "",
    int AppraisalType = 0,
    decimal AppraisalValue = 0m,
    int MethodOfAppraisal = 0,
    decimal ForceSaleValue = 0m,
    string LandOffice = "",
    int? Decorate = null,
    string Developer = "",
    decimal AppraisalValueWaOrM = 0.0m,
    string AppraisalReportNo = "",
    decimal AppraisalFee = 0m,
    decimal LandValue = 0m,
    string SequenceOfApprove = "",
    decimal BuildingValue = 0m,
    string AppraisalDate = "",
    decimal MarketValue = 0m);

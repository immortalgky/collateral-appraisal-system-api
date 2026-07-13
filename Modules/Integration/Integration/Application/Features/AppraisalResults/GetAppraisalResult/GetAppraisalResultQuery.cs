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

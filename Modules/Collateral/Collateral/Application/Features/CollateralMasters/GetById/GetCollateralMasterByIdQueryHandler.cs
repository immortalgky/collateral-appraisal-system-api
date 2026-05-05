using Collateral.Application.Features.CollateralMasters.Lookup;
using Collateral.Application.Features.CollateralMasters.Shared;
using Dapper;

namespace Collateral.Application.Features.CollateralMasters.GetById;

/// <summary>
/// Returns the full master detail from vw_CollateralMasters.
/// For Leasehold masters, also fetches a lightweight underlying master summary.
/// </summary>
public class GetCollateralMasterByIdQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetCollateralMasterByIdQuery, GetCollateralMasterByIdResult>
{
    public async Task<GetCollateralMasterByIdResult> Handle(
        GetCollateralMasterByIdQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM collateral.vw_CollateralMasters WHERE Id = @Id";
        var row = await connectionFactory.QueryFirstOrDefaultAsync<CollateralMasterViewRow>(
            sql, new { query.Id });

        if (row is null)
            throw new NotFoundException("CollateralMaster", query.Id);

        UnderlyingMasterSummaryDto? underlyingMaster = null;

        if (row.CollateralType == CollateralTypes.Leasehold && row.Lh_UnderlyingMasterId.HasValue)
        {
            underlyingMaster = await FetchUnderlyingMasterSummaryAsync(row.Lh_UnderlyingMasterId.Value);
        }

        return MapToResult(row, underlyingMaster);
    }

    private async Task<UnderlyingMasterSummaryDto?> FetchUnderlyingMasterSummaryAsync(Guid underlyingId)
    {
        var sql = """
            SELECT
                Id,
                CollateralType,
                OwnerName,
                Land_Province     AS Province,
                Land_TitleDeedNo  AS TitleDeedNo,
                LastAppraisedDate,
                LastAppraisedValue
            FROM collateral.vw_CollateralMasters
            WHERE Id = @Id
            """;

        var row = await connectionFactory.QueryFirstOrDefaultAsync<UnderlyingRow>(
            sql, new { Id = underlyingId });

        if (row is null) return null;

        return new UnderlyingMasterSummaryDto(
            row.Id,
            row.CollateralType,
            row.OwnerName,
            row.Province,
            row.TitleDeedNo,
            row.LastAppraisedDate,
            row.LastAppraisedValue);
    }

    private static GetCollateralMasterByIdResult MapToResult(
        CollateralMasterViewRow row,
        UnderlyingMasterSummaryDto? underlying)
    {
        LandDetailDto? landDetail = null;
        CondoDetailDto? condoDetail = null;
        LeaseholdDetailDto? leaseholdDetail = null;
        MachineDetailDto? machineDetail = null;

        switch (row.CollateralType)
        {
            case CollateralTypes.Land:
                landDetail = new LandDetailDto(
                    row.Land_LandOfficeCode!,
                    row.Land_Province!,
                    row.Land_Amphur!,
                    row.Land_Tambon!,
                    row.Land_TitleDeedType!,
                    row.Land_TitleDeedNo!,
                    row.Land_SurveyOrParcelNo,
                    row.Land_Street,
                    row.Land_Village,
                    row.Land_PostalCode,
                    row.Land_Latitude,
                    row.Land_Longitude,
                    row.Land_LandShapeType,
                    row.Land_LandZoneType,
                    row.Land_UrbanPlanningType,
                    row.Land_AccessRoadWidth,
                    row.Land_RoadFrontage,
                    row.Land_LandArea,
                    row.IsUnderConstructionAtLastAppraisal ?? false,
                    row.OverallConstructionProgressPercent,
                    row.Land_LastConstructionInspectionId,
                    row.Land_LastAppraisalId,
                    row.Land_LastAppraisalNumber,
                    row.Land_LastAppraisedDate,
                    row.Land_LastAppraisedValue,
                    row.Land_LastTotalAppraisedValue,
                    AliasTitles: []);   // GetById does not load alias titles — FE uses Lookup for that
                break;

            case CollateralTypes.Condo:
                condoDetail = new CondoDetailDto(
                    row.Condo_LandOfficeCode!,
                    row.Condo_CondoRegistrationNumber!,
                    row.Condo_BuildingNumber!,
                    row.Condo_FloorNumber!,
                    row.Condo_UnitNumber!,
                    row.Condo_TitleNumber!,
                    row.Condo_TitleType!,
                    row.Condo_CondoName,
                    row.Condo_Province,
                    row.Condo_UsableArea,
                    row.Condo_LocationType,
                    row.Condo_BuildingAge,
                    row.Condo_ConstructionYear,
                    row.Condo_ModelName,
                    row.Condo_LastAppraisalId,
                    row.Condo_LastAppraisalNumber,
                    row.Condo_LastAppraisedDate,
                    row.Condo_LastAppraisedValue);
                break;

            case CollateralTypes.Leasehold:
                leaseholdDetail = new LeaseholdDetailDto(
                    row.Lh_LeaseRegistrationNo!,
                    row.Lh_UnderlyingMasterId!.Value,
                    row.Lh_Lessor!,
                    row.Lh_Lessee!,
                    DateOnly.FromDateTime(row.Lh_LeaseTermStart!.Value),
                    row.Lh_LeaseTermEnd.HasValue ? DateOnly.FromDateTime(row.Lh_LeaseTermEnd.Value) : null,
                    row.Lh_LeaseTermMonths,
                    row.Lh_AnnualRent,
                    row.Lh_LeasePurpose,
                    row.Lh_LastAppraisalId,
                    row.Lh_LastAppraisalNumber,
                    row.Lh_LastAppraisedDate,
                    row.Lh_LastAppraisedValue);
                break;

            case CollateralTypes.Machine:
                machineDetail = new MachineDetailDto(
                    row.Machine_MachineRegistrationNo,
                    row.Machine_SerialNo,
                    row.Machine_Brand,
                    row.Machine_Model,
                    row.Machine_Manufacturer,
                    row.Machine_EngineNo,
                    row.Machine_ChassisNo,
                    row.Machine_YearOfManufacture,
                    row.Machine_MachineCondition,
                    row.Machine_MachineAge,
                    row.Machine_LastAppraisalId,
                    row.Machine_LastAppraisalNumber,
                    row.Machine_LastAppraisedDate,
                    row.Machine_LastAppraisedValue);
                break;
        }

        return new GetCollateralMasterByIdResult(
            row.Id,
            row.CollateralType,
            row.OwnerName,
            row.CreatedOn,
            row.UpdatedOn,
            row.EngagementCount ?? 0,
            row.LastAppraisedDate,
            row.LastAppraisedValue,
            landDetail,
            condoDetail,
            leaseholdDetail,
            machineDetail,
            underlying);
    }

    // Small Dapper projection for the underlying master summary query
    private class UnderlyingRow
    {
        public Guid Id { get; init; }
        public string CollateralType { get; init; } = null!;
        public string? OwnerName { get; init; }
        public string? Province { get; init; }
        public string? TitleDeedNo { get; init; }
        public DateTime? LastAppraisedDate { get; init; }
        public decimal? LastAppraisedValue { get; init; }
    }
}

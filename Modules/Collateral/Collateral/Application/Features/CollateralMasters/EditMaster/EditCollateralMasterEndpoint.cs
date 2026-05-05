using Collateral.CollateralMasters.Models;

namespace Collateral.Application.Features.CollateralMasters.EditMaster;

/// <summary>
/// PATCH /collateral-masters/{id}
/// Admin-only. Updates identity / last-known fields and writes an audit log row.
/// Rejects with 409 if the new dedup key collides with another master.
/// Rejects with 400 if Reason is empty.
/// </summary>
public class EditCollateralMasterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/collateral-masters/{id}",
                async (
                    Guid id,
                    EditCollateralMasterRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new EditCollateralMasterCommand(
                        id,
                        request.OwnerName,
                        request.Reason,
                        request.LandDetail is null
                            ? null
                            : new LandAdminEdit(
                                request.LandDetail.LandOfficeCode,
                                request.LandDetail.Province,
                                request.LandDetail.Amphur,
                                request.LandDetail.Tambon,
                                request.LandDetail.TitleDeedType,
                                request.LandDetail.TitleDeedNo,
                                request.LandDetail.SurveyOrParcelNo,
                                request.LandDetail.Street,
                                request.LandDetail.Village,
                                request.LandDetail.PostalCode,
                                request.LandDetail.Latitude,
                                request.LandDetail.Longitude,
                                request.LandDetail.LandShapeType,
                                request.LandDetail.LandZoneType,
                                request.LandDetail.UrbanPlanningType,
                                request.LandDetail.AccessRoadWidth,
                                request.LandDetail.RoadFrontage,
                                request.LandDetail.LandArea),
                        request.CondoDetail is null
                            ? null
                            : new CondoAdminEdit(
                                request.CondoDetail.LandOfficeCode,
                                request.CondoDetail.CondoRegistrationNumber,
                                request.CondoDetail.BuildingNumber,
                                request.CondoDetail.FloorNumber,
                                request.CondoDetail.UnitNumber,
                                request.CondoDetail.TitleNumber,
                                request.CondoDetail.TitleType,
                                request.CondoDetail.CondoName,
                                request.CondoDetail.Province,
                                request.CondoDetail.UsableArea,
                                request.CondoDetail.LocationType,
                                request.CondoDetail.BuildingAge,
                                request.CondoDetail.ConstructionYear,
                                request.CondoDetail.ModelName),
                        request.LeaseholdDetail is null
                            ? null
                            : new LeaseholdAdminEdit(
                                request.LeaseholdDetail.LeaseRegistrationNo,
                                request.LeaseholdDetail.Lessor,
                                request.LeaseholdDetail.Lessee,
                                request.LeaseholdDetail.LeaseTermStart,
                                request.LeaseholdDetail.LeaseTermEnd,
                                request.LeaseholdDetail.LeaseTermMonths,
                                request.LeaseholdDetail.AnnualRent,
                                request.LeaseholdDetail.LeasePurpose),
                        request.MachineDetail is null
                            ? null
                            : new MachineAdminEdit(
                                request.MachineDetail.MachineRegistrationNo,
                                request.MachineDetail.SerialNo,
                                request.MachineDetail.Brand,
                                request.MachineDetail.Model,
                                request.MachineDetail.Manufacturer,
                                request.MachineDetail.EngineNo,
                                request.MachineDetail.ChassisNo,
                                request.MachineDetail.YearOfManufacture,
                                request.MachineDetail.MachineCondition,
                                request.MachineDetail.MachineAge));

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("EditCollateralMaster")
            .Produces<EditCollateralMasterResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Edit collateral master fields (admin)")
            .WithDescription("Updates identity and last-known fields. Requires Reason. Rejects dedup-key collision with 409.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}

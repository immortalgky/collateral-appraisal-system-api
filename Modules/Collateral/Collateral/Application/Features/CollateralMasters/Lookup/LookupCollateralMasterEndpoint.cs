namespace Collateral.Application.Features.CollateralMasters.Lookup;

/// <summary>
/// GET /collateral-masters/lookup?type={Land|Condo|Leasehold|Machine}&amp;...dedup params
/// Authenticated. Returns 200+body or 404.
/// </summary>
public class LookupCollateralMasterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collateral-masters/lookup",
                async (
                    string type,
                    // Land
                    string? landOfficeCode,
                    string? province,
                    string? amphur,
                    string? tambon,
                    string? titleDeedType,
                    string? titleDeedNo,
                    string? surveyOrParcelNo,
                    // Condo
                    string? condoRegistrationNumber,
                    string? building,
                    string? floor,
                    string? unit,
                    string? titleNumber,
                    string? titleType,
                    // Leasehold
                    string? contractNo,
                    Guid? underlyingMasterId,
                    string? lessor,
                    string? lessee,
                    DateOnly? leaseTermStart,
                    // Machine tier-1
                    string? machineRegistrationNo,
                    // Machine tier-2
                    string? serialNo,
                    string? brand,
                    string? model,
                    string? manufacturer,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new LookupCollateralMasterQuery(
                        type,
                        landOfficeCode, province, amphur, tambon, titleDeedType, titleDeedNo, surveyOrParcelNo,
                        condoRegistrationNumber, building, floor, unit, titleNumber, titleType,
                        contractNo, underlyingMasterId, lessor, lessee, leaseTermStart,
                        machineRegistrationNo,
                        serialNo, brand, model, manufacturer);

                    try
                    {
                        var result = await sender.Send(query, cancellationToken);
                        return Results.Ok(result);
                    }
                    catch (NotFoundException)
                    {
                        return Results.NotFound();
                    }
                }
            )
            .WithName("LookupCollateralMaster")
            .Produces<LookupCollateralMasterResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Lookup collateral master by dedup key")
            .WithDescription("Returns the collateral master matching the type-specific dedup key, or 404 if not found.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}

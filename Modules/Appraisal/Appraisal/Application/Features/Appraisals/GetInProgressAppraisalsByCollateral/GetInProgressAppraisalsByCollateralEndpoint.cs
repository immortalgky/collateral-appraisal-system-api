using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.Appraisals.GetInProgressAppraisalsByCollateral;

/// <summary>
/// GET /appraisals/in-progress-by-collateral?type={L|LB|U|MAC}&amp;...dedup params
/// Same parameters as GET /collateral-masters/lookup. Authenticated, like the lookup it warns beside.
/// </summary>
public class GetInProgressAppraisalsByCollateralEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/in-progress-by-collateral",
                async (
                    string type,
                    string? province,
                    string? district,
                    string? subDistrict,
                    string? titleNumber,
                    string? condoRegistrationNumber,
                    string? building,
                    string? floor,
                    string? unit,
                    string? machineRegistrationNo,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new GetInProgressAppraisalsByCollateralQuery(
                            type, province, district, subDistrict, titleNumber,
                            condoRegistrationNumber, building, floor, unit, machineRegistrationNo),
                        cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetInProgressAppraisalsByCollateral")
            .Produces<IReadOnlyList<InProgressAppraisalDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Appraisals still in the workflow that hold this collateral")
            .WithDescription(
                "Returns the distinct appraisals not yet Completed/Cancelled/Rejected whose properties carry "
                + "the given collateral key (same key as the collateral lookup). Incomplete key returns an empty list.")
            .WithTags("Appraisal")
            .RequireAuthorization();
    }
}

using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalSuspensions;

public class GetAppraisalSuspensionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/appraisals/suspensions",
            async (
                [AsParameters] PaginationRequest pagination,
                [FromQuery] string? appraisalNumber,
                [FromQuery] SuspensionFilter? status,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetAppraisalSuspensionsQuery(appraisalNumber, status ?? SuspensionFilter.All, pagination),
                    cancellationToken);

                return Results.Ok(result);
            })
            .WithName("GetAppraisalSuspensions")
            .Produces<PaginatedResult<AppraisalSuspensionRow>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Completed appraisals for the Appraisal Suspension screen (paged)")
            .WithDescription(
                "appraisalNumber: substring match. status: All (default) | Active | Suspended. " +
                "Without appraisalNumber, only status=Suspended returns rows.")
            .WithTags("Appraisal Suspension")
            .RequireAuthorization("appraisal.suspension");
    }
}

namespace Appraisal.Application.Features.Appraisals.GetAppraisalSuspensionLogs;

public class GetAppraisalSuspensionLogsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/appraisals/{appraisalId:guid}/suspension-logs",
            async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetAppraisalSuspensionLogsQuery(appraisalId), cancellationToken);

                return Results.Ok(result);
            })
            .WithName("GetAppraisalSuspensionLogs")
            .Produces<IReadOnlyList<AppraisalSuspensionLogDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Suspend/reactivate history of one appraisal, newest first")
            .WithTags("Appraisal Suspension")
            .RequireAuthorization("appraisal.suspension");
    }
}

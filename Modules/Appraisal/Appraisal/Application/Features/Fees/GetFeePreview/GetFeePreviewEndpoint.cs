namespace Appraisal.Application.Features.Fees.GetFeePreview;

public class GetFeePreviewEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/fee-preview",
                async (
                    Guid appraisalId,
                    string? assignmentType,
                    string? assignmentMethod,
                    Guid? companyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) => Results.Ok(await sender.Send(
                    new GetFeePreviewQuery(appraisalId, assignmentType, assignmentMethod, companyId),
                    cancellationToken))
            )
            .WithName("GetFeePreview")
            .Produces<FeePreviewResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Preview the appraisal fee")
            .WithDescription(
                "Read-only: the fee that assignment would create for the given assignment type " +
                "(internal | external | external-offline), method (manual | roundrobin | quotation) and company. " +
                "Omitted parameters fall back to the appraisal's current assignment. Nothing is written.")
            .WithTags("Fee");
    }
}

using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.DecisionSummary.SaveDecisionSummary;

public class SaveDecisionSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/decision-summary",
                async (
                    Guid appraisalId,
                    SaveDecisionSummaryRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SaveDecisionSummaryCommand(
                        appraisalId,
                        request.IsPriceVerified,
                        request.ConditionType,
                        request.Condition,
                        request.RemarkType,
                        request.Remark,
                        request.AppraiserOpinionType,
                        request.AppraiserOpinion,
                        request.CommitteeOpinionType,
                        request.CommitteeOpinion,
                        request.TotalAppraisalPriceReview,
                        request.AdditionalAssumptions);

                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<SaveDecisionSummaryResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SaveDecisionSummary")
            .Produces<SaveDecisionSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Save decision summary")
            .WithDescription("Creates or updates the decision summary for an appraisal.")
            .WithTags("DecisionSummary");
    }
}

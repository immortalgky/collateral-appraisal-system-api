using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.DecisionSummary.GetDecisionSummary;

public class GetDecisionSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/decision-summary",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetDecisionSummaryQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetDecisionSummaryResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetDecisionSummary")
            .Produces<GetDecisionSummaryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get decision summary")
            .WithDescription("Retrieves the decision summary for an appraisal, including calculated pricing data and stored decision fields.")
            .WithTags("DecisionSummary");
    }
}

using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetMachinerySummary;

public class GetMachinerySummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/machinery-summary",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetMachinerySummaryQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetMachinerySummaryResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetMachinerySummary")
            .Produces<GetMachinerySummaryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get machinery appraisal summary")
            .WithDescription("Retrieves the machinery appraisal summary (Section 3.1 & 3.3) for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}

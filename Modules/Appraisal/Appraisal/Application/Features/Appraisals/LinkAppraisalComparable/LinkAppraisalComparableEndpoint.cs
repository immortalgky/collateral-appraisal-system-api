using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.LinkAppraisalComparable;

public class LinkAppraisalComparableEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/appraisals/{appraisalId:guid}/comparables",
            async (Guid appraisalId, LinkAppraisalComparableRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new LinkAppraisalComparableCommand(
                    appraisalId,
                    request.MarketComparableId,
                    request.SequenceNumber,
                    request.OriginalPricePerUnit,
                    request.Weight,
                    request.SelectionReason,
                    request.Notes);

                var result = await sender.Send(command, cancellationToken);
                var response = result.Adapt<LinkAppraisalComparableResponse>();
                return Results.Created($"/appraisals/{appraisalId}/comparables/{response.Id}", response);
            })
            .WithName("LinkAppraisalComparable")
            .Produces<LinkAppraisalComparableResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Link a market comparable to an appraisal")
            .WithDescription("Links a market comparable to an appraisal for valuation comparison.")
            .WithTags("AppraisalComparables");
    }
}

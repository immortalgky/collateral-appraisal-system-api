using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.CreateOrUpdateMachinerySummary;

public class CreateOrUpdateMachinerySummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/machinery-summary",
                async (
                    Guid appraisalId,
                    CreateOrUpdateMachinerySummaryRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateOrUpdateMachinerySummaryCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateOrUpdateMachinerySummaryResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateOrUpdateMachinerySummary")
            .Produces<CreateOrUpdateMachinerySummaryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create or update machinery appraisal summary")
            .WithDescription("Creates or updates the machinery appraisal summary (Section 3.1 & 3.3) for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}

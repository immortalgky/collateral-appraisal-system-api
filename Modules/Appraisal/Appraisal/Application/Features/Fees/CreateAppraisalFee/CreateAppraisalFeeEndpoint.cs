using Mapster;

namespace Appraisal.Application.Features.Fees.CreateAppraisalFee;

public class CreateAppraisalFeeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/fees",
                async (
                    Guid appraisalId,
                    CreateAppraisalFeeRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CreateAppraisalFeeCommand(
                        appraisalId,
                        request.AssignmentId,
                        request.BankAbsorbAmount);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateAppraisalFeeResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("CreateAppraisalFee")
            .Produces<CreateAppraisalFeeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create appraisal fee")
            .WithDescription("Create a fee record for an assignment using active fee structures.")
            .WithTags("Fee");
    }
}

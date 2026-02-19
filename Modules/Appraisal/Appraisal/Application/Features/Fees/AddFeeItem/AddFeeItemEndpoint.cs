namespace Appraisal.Application.Features.Fees.AddFeeItem;

public class AddFeeItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/fees/{feeId:guid}/items",
                async (
                    Guid appraisalId,
                    Guid feeId,
                    AddFeeItemRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddFeeItemCommand(
                        appraisalId,
                        feeId,
                        request.FeeCode,
                        request.FeeDescription,
                        request.FeeAmount);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("AddFeeItem")
            .Produces<AddFeeItemResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add fee item")
            .WithDescription("Add an individual fee item (e.g. Travel Fee, Urgent Fee) to an existing appraisal fee.")
            .WithTags("Fee");
    }
}

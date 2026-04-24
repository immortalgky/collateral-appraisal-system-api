namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public class FinalizeQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/finalize",
                async (
                    Guid id,
                    FinalizeQuotationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new FinalizeQuotationCommand(
                        id,
                        request.CompanyQuotationId,
                        request.FinalPrice,
                        request.Reason);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("FinalizeQuotation")
            .Produces<FinalizeQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Finalize quotation")
            .WithDescription("Admin commits the final price and marks the quotation as Finalized. Triggers ext assignment creation and workflow task completion (Track 2).")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

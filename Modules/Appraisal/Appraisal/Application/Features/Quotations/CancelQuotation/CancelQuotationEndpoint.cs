namespace Appraisal.Application.Features.Quotations.CancelQuotation;

public class CancelQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/cancel",
                async (
                    Guid id,
                    CancelQuotationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CancelQuotationCommand(id, request.Reason);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("CancelQuotation")
            .Produces<CancelQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Cancel quotation")
            .WithDescription("Admin cancels the quotation request. Not allowed when status is Finalized.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

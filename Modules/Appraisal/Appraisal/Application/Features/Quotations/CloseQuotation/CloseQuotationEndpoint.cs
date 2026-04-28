namespace Appraisal.Application.Features.Quotations.CloseQuotation;

public class CloseQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/close",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new CloseQuotationCommand(id), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("CloseQuotation")
            .Produces<CloseQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Close quotation for new submissions")
            .WithDescription("Transitions a QuotationRequest from Sent to UnderAdminReview. Idempotent.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

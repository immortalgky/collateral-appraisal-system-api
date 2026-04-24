namespace Appraisal.Application.Features.Quotations.SendQuotation;

/// <summary>
/// POST /quotations/{id}/send — Admin explicitly sends a Draft quotation to invited companies.
/// C8: Separate from the Draft creation so admin can assemble (add appraisals, adjust invitations) before committing.
/// </summary>
public class SendQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/send",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SendQuotationCommand(id);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("SendQuotation")
            .Produces<SendQuotationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Send quotation to invited companies")
            .WithDescription("Admin explicitly sends a Draft quotation. Transitions to Sent and notifies all invited companies. Emits QuotationStartedIntegrationEvent.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

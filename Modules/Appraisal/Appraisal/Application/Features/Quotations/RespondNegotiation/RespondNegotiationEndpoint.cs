namespace Appraisal.Application.Features.Quotations.RespondNegotiation;

public class RespondNegotiationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/negotiations/{negotiationId:guid}/respond",
                async (
                    Guid id,
                    Guid negotiationId,
                    RespondNegotiationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RespondNegotiationCommand(
                        id,
                        negotiationId,
                        request.CompanyQuotationId,
                        request.Verb,
                        request.CounterPrice,
                        request.Message,
                        request.Items);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("RespondNegotiation")
            .Produces<RespondNegotiationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Respond to negotiation round")
            .WithDescription("External company responds to an open negotiation round with Accept, Counter, or Reject.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

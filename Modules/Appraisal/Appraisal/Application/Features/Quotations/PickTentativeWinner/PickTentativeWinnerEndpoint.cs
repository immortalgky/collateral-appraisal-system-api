namespace Appraisal.Application.Features.Quotations.PickTentativeWinner;

public class PickTentativeWinnerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/pick-tentative-winner",
                async (
                    Guid id,
                    PickTentativeWinnerRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new PickTentativeWinnerCommand(id, request.CompanyQuotationId, request.Reason,
                        request.RequestNegotiation, request.NegotiationNote);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("PickTentativeWinner")
            .Produces<PickTentativeWinnerResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Pick tentative winner")
            .WithDescription("RM (or Admin) picks a shortlisted company as the tentative winner.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

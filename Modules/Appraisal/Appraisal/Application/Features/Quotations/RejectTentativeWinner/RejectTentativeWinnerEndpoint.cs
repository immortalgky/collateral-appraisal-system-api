namespace Appraisal.Application.Features.Quotations.RejectTentativeWinner;

public class RejectTentativeWinnerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/reject-tentative-winner",
                async (
                    Guid id,
                    RejectTentativeWinnerRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RejectTentativeWinnerCommand(id, request.Reason);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("RejectTentativeWinner")
            .Produces<RejectTentativeWinnerResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reject the tentative winner")
            .WithDescription("Admin rejects the tentative winner and returns the quotation to UnderAdminReview for re-shortlisting.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

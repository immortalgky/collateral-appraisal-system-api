namespace Appraisal.Application.Features.Quotations.SendShortlistToRm;

public class SendShortlistToRmEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/send-to-rm",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new SendShortlistToRmCommand(id), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("SendShortlistToRm")
            .Produces<SendShortlistToRmResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Send shortlist to RM for selection")
            .WithDescription("Admin sends the shortlisted quotations to the RM for tentative winner selection.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

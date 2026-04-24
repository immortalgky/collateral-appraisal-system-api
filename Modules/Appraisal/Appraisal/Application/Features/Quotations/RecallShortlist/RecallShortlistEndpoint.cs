namespace Appraisal.Application.Features.Quotations.RecallShortlist;

public class RecallShortlistEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/recall-shortlist",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new RecallShortlistCommand(id), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("RecallShortlist")
            .Produces<RecallShortlistResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Recall shortlist from RM")
            .WithDescription("Admin recalls the shortlist from RM, returning to UnderAdminReview. Not allowed if a tentative winner is already picked.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

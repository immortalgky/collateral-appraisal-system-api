using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.SubmitDraftToChecker;

public class SubmitDraftToCheckerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/submit-to-checker",
                async (
                    Guid id,
                    ISender sender,
                    ICurrentUserService currentUserService,
                    CancellationToken cancellationToken
                ) =>
                {
                    // CompanyId is derived from the JWT company_id claim — never from the URL.
                    var companyId = currentUserService.CompanyId
                        ?? throw new UnauthorizedAccessException(
                            "External company user has no company_id claim");

                    var command = new SubmitDraftToCheckerCommand(
                        QuotationRequestId: id,
                        CompanyId: companyId);

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("SubmitDraftToChecker")
            .Produces<SubmitDraftToCheckerResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Submit draft quotation to checker")
            .WithDescription(
                "Maker (ExtAdmin) promotes their saved draft to PendingCheckerReview. " +
                "The company is identified from the JWT company_id claim. " +
                "Only users with the ExtAdmin role may call this endpoint.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

namespace Appraisal.Application.Features.Quotations.DeclineInvitation;

public class DeclineInvitationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/{id:guid}/companies/{companyId:guid}/decline",
                async (
                    Guid id,
                    Guid companyId,
                    DeclineInvitationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new DeclineInvitationCommand(id, companyId, request.Reason);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("DeclineQuotationInvitation")
            .Produces<DeclineInvitationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Decline a quotation invitation")
            .WithDescription("External company user declines the quotation invitation. " +
                             "If all other companies have already responded, triggers early close to UnderAdminReview.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

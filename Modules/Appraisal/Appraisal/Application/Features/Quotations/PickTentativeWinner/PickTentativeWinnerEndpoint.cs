using Appraisal.Application.Features.Quotations.Shared;
using Shared.Identity;

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
                    ICurrentUserService currentUser,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var role = currentUser.IsInRole("RequestMaker") ? "RequestMaker"
                             : currentUser.IsInRole("IntAdmin")     ? "IntAdmin"
                             : currentUser.IsInRole("Admin")        ? "Admin"
                             : throw new UnauthorizedAccessException("Caller has no recognised role for this action");

                    var actor = new QuotationActor(
                        Username: currentUser.Username ?? throw new UnauthorizedAccessException("Cannot resolve username from token"),
                        Role: role,
                        UserId: currentUser.UserId);

                    var command = new PickTentativeWinnerCommand(id, request.CompanyQuotationId, actor,
                        request.Reason, request.RequestNegotiation, request.NegotiationNote);
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

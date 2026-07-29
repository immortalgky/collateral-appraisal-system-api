namespace Appraisal.Application.Features.Assignments.SetOfflineExternalEngagement;

public class SetOfflineExternalEngagementEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/assignments/offline-external-engagement",
                async (
                    Guid appraisalId,
                    SetOfflineExternalEngagementRequest request,
                    ICurrentUserService currentUser,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    // The actor is taken from the token, never from the body. AssignedBy is an audit
                    // field on a row that drives fee materialisation and the AS400 feed; accepting a
                    // client-supplied value let any caller attribute an engagement to someone else.
                    var command = new SetOfflineExternalEngagementCommand(
                        appraisalId,
                        request.CompanyId,
                        request.BookDate,
                        request.ExternalAppraiserName,
                        currentUser.UserCode ?? string.Empty);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SetOfflineExternalEngagementResponse>();
                    return Results.Ok(response);
                }
            )
            .RequireAuthorization()
            .WithName("SetOfflineExternalEngagement")
            .Produces<SetOfflineExternalEngagementResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Record an off-system external engagement")
            .WithDescription(
                "Records the external company that appraised the collateral outside the system and " +
                "the appraisal date printed on its book, then materialises the assignment fee. " +
                "Used from the int-offline-book-keyin task.")
            .WithTags("Assignment");
    }
}

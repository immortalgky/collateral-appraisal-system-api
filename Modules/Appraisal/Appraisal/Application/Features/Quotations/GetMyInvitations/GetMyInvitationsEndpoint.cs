namespace Appraisal.Application.Features.Quotations.GetMyInvitations;

public class GetMyInvitationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/quotations/my-invitations",
                async (
                    [AsParameters] PaginationRequest request,
                    string? status,
                    string? search,
                    DateOnly? dueDateFrom,
                    DateOnly? dueDateTo,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetMyInvitationsQuery(request, status, search, dueDateFrom, dueDateTo);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetMyInvitationsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetMyInvitations")
            .Produces<GetMyInvitationsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get my company's quotation invitations")
            .WithDescription("Returns quotation invitations for the caller's company with per-company status derived from the RFQ and invitation state.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

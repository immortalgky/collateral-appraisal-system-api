namespace Appraisal.Application.Features.CommitteeVoting.GetApprovalList;

public class GetApprovalListEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/approval-list",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetApprovalListQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetApprovalListResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetApprovalList")
            .Produces<GetApprovalListResponse>()
            .WithSummary("Get approval list")
            .WithDescription("Returns committee members and their vote status for an appraisal's committee review.")
            .WithTags("CommitteeVoting");
    }
}

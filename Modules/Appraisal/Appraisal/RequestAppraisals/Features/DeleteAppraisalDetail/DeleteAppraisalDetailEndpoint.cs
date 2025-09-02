namespace Appraisal.RequestAppraisals.Features.DeleteAppraisalDetail;

public class DeleteAppraisalDetailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/requests/collateral/appraisal/{apprId:long}",
            async (long apprId, ISender sender) =>
        {
            var command = new DeleteAppraisalDetailCommand(apprId);

            var result = await sender.Send(command);

            var response = result.Adapt<DeleteAppraisalDetailResponse>();

            return Results.Ok(response);
        });
    }
}
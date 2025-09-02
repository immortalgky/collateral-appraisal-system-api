namespace Appraisal.RequestAppraisals.Features.UpdateAppraisalDetail;

public class UpdateAppraisalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/requests/collateral/appraisal/{apprId:long}",
            async ([FromBody] UpdateAppraisalDetailRequest request, long apprId, ISender sender) =>
            {
                var command = new UpdateAppraisalDetailCommand(apprId, request.RequestAppraisal);

                var result = await sender.Send(command);

                var response = result.Adapt<UpdateAppraisalDetailResponse>();

                return Results.Ok(response);
            });
    }
}
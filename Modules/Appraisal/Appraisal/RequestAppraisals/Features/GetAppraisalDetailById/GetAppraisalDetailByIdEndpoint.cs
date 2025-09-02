namespace Appraisal.RequestAppraisals.Features.GetAppraisalDetailById;

public class GetAppraisalDetailByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/collateral/appraisal/{apprId:long}",
            async (long apprId, ISender sender) =>
            {
                var query = new GetAppraisalDetailByIdQuery(apprId);

                var result = await sender.Send(query);

                var response = result.Adapt<GetAppraisalDetailByIdResponse>();

                return Results.Ok(response);
            });
    }
}
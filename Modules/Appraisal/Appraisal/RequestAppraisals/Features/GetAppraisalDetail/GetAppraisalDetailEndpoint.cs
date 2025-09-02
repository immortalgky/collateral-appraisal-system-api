namespace Appraisal.RequestAppraisals.Features.GetAppraisalDetail;

    public class GetAppraisalDetailEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("requests/appraisals", async ([AsParameters] PaginationRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetAppraisalDetailQuery(request), cancellationToken);

                return Results.Ok(result.Appraisal);
            });
        }
    }
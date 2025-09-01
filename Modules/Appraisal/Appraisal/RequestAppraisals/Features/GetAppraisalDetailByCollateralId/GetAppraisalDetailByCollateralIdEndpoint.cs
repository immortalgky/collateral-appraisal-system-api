using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.RequestAppraisals.Features.GetAppraisalDetailByCollateralId;

public sealed class GetAppraisalDetailByCollateralIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/request/collateral/appraisal/{collatId:long}/collateral",
            async (long collatId, ISender sender) =>
        {
            var query = new GetAppraisalDetailByCollateralIdQuery(collatId);

            var result = await sender.Send(query);

            var response = result.Adapt<GetAppraisalDetailByCollateralIdResponse>();

            return Results.Ok(response);
        });
    }
}
using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.RequestAppraisals.Features.CreateAppraisalDetail;

public class CreateAppraisalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:long}/collateral/{collateralId:long}/appraisal",
            async (CreateAppraisalDetailRequest request, long requestId, long collateralId, ISender sender) =>
        {
            var command = new CreateAppraisalDetailCommand(request.RequestAppraisal, requestId, collateralId);

            var result = await sender.Send(command);

            var response = result.Adapt<CreateAppraisalDetailResponse>();

            return Results.Ok(response);
        });
    }
}
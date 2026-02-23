using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.Requests.UpdateRequest;

namespace Integration.Application.Features.AppraisalRequests.ResubmitRequest;

public class ResubmitRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/requests/{requestId:guid}/resubmit", 
        async (Guid requestId, UpdateRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = request.Adapt<ResubmitRequestCommand>() with { RequestId = requestId };

            var result = await sender.Send(command, cancellationToken);
            return Results.Ok(result);
        })
          .WithName("ResubmitRequest")
          .Produces<ResubmitRequestResult>()
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .WithTags("Requests")
          .WithSummary("Resubmit an existing request")
          .WithDescription("Resubmits an existing request in the system.")
          .AllowAnonymous();;
    }
}

using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.RequestComments.GetRequestCommentsByRequestId;

namespace Api.Endpoints.RequestComments.GetRequestCommentsByRequestId;

public class GetRequestCommentsByRequestIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:guid}/comments",
                async (Guid requestId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetRequestCommentsByRequestIdQuery(requestId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetRequestCommentsByRequestIdResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetRequestCommentsByRequestId")
            .Produces<GetRequestCommentsByRequestIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Request Comments")
            .WithSummary("Get all comments for a request")
            .WithDescription("Retrieves all comments associated with the specified request.")
            .AllowAnonymous();
    }
}

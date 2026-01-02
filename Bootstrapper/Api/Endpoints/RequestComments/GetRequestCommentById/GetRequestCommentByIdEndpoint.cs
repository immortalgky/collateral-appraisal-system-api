using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.RequestComments.GetRequestCommentById;

namespace Api.Endpoints.RequestComments.GetRequestCommentById;

public class GetRequestCommentByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:guid}/comments/{commentId:guid}",
                async (Guid requestId, Guid commentId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetRequestCommentByIdQuery(requestId, commentId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetRequestCommentByIdResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetRequestCommentById")
            .Produces<GetRequestCommentByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Request Comments")
            .WithSummary("Get request comment by ID")
            .WithDescription("Retrieves a specific comment by its ID.")
            .AllowAnonymous();
    }
}

using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.RequestComments.RemoveRequestComment;

namespace Api.Endpoints.RequestComments.RemoveRequestComment;

public class RemoveRequestCommentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/requests/{requestId:guid}/comments/{commentId:guid}",
                async (Guid requestId, Guid commentId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new RemoveRequestCommentCommand(commentId), cancellationToken);
                    var response = result.Adapt<RemoveRequestCommentResponse>();
                    return Results.Ok(response);
                })
            .WithName("RemoveRequestComment")
            .Produces<RemoveRequestCommentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Request Comments")
            .WithSummary("Remove a comment from a request")
            .WithDescription("Removes an existing comment from the specified request.")
            .AllowAnonymous();
    }
}

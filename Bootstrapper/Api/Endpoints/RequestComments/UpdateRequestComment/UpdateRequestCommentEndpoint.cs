using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.RequestComments.UpdateRequestComment;

namespace Api.Endpoints.RequestComments.UpdateRequestComment;

public class UpdateRequestCommentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{requestId:guid}/comments/{commentId:guid}",
                async (Guid requestId, Guid commentId, UpdateRequestCommentRequest request, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new UpdateRequestCommentCommand(commentId, request.Comment);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<UpdateRequestCommentResponse>();
                    return Results.Ok(response);
                })
            .WithName("UpdateRequestComment")
            .Produces<UpdateRequestCommentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Request Comments")
            .WithSummary("Update a request comment")
            .WithDescription("Updates an existing comment on the specified request.")
            .AllowAnonymous();
    }
}

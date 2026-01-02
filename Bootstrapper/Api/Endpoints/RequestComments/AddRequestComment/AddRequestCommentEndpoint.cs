using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.RequestComments.AddRequestComment;

namespace Api.Endpoints.RequestComments.AddRequestComment;

public class AddRequestCommentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:guid}/comments",
                async (Guid requestId, AddRequestCommentRequest request, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new AddRequestCommentCommand(requestId, request.Comment, request.CommentedBy,
                        request.CommentedByName);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<AddRequestCommentResponse>();
                    return Results.Ok(response);
                })
            .WithName("AddRequestComment")
            .Produces<AddRequestCommentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Request Comments")
            .WithSummary("Add a comment to a request")
            .WithDescription("Adds a new comment to the specified request.")
            .AllowAnonymous();
    }
}

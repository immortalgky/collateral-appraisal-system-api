using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public record CreateUploadSessionRequest(
    string? ExternalReference,
    int ExpectedDocumentCount = 1
);

public record CreateUploadSessionResponse(
    Guid SessionId,
    DateTime ExpiresAt
);

public class CreateUploadSessionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/upload-sessions", async (
            CreateUploadSessionRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateUploadSessionCommand(
                request.ExternalReference,
                request.ExpectedDocumentCount
            );

            var result = await sender.Send(command, cancellationToken);

            return Results.Created(
                $"/api/v1/upload-sessions/{result.SessionId}",
                new CreateUploadSessionResponse(result.SessionId, result.ExpiresAt)
            );
        })
        .WithName("CreateUploadSession")
        .WithTags("Integration - Upload Sessions")
        .Produces<CreateUploadSessionResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}

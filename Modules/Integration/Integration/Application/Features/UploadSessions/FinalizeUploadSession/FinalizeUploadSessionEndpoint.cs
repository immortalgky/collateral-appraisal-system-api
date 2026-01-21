using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.UploadSessions.FinalizeUploadSession;

public record FinalizeUploadSessionResponse(
    Guid SessionId,
    string Status,
    List<Guid> DocumentIds
);

public class FinalizeUploadSessionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/upload-sessions/{id:guid}/finalize", async (
            Guid id,
            HttpRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var idempotencyKey = request.Headers["X-Idempotency-Key"].FirstOrDefault();

            var command = new FinalizeUploadSessionCommand(id, idempotencyKey);
            var result = await sender.Send(command, cancellationToken);

            return Results.Ok(new FinalizeUploadSessionResponse(
                result.SessionId,
                result.Status,
                result.DocumentIds
            ));
        })
        .WithName("FinalizeUploadSession")
        .WithTags("Integration - Upload Sessions")
        .Produces<FinalizeUploadSessionResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

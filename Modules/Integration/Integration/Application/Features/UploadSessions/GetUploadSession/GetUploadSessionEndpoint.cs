using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.UploadSessions.GetUploadSession;

public record GetUploadSessionResponse(
    Guid SessionId,
    string Status,
    int TotalDocuments,
    long TotalSizeBytes,
    DateTime? CompletedAt,
    DateTime ExpiresAt,
    string? ExternalReference
);

public class GetUploadSessionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/upload-sessions/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUploadSessionQuery(id);
            var result = await sender.Send(query, cancellationToken);

            return Results.Ok(new GetUploadSessionResponse(
                result.SessionId,
                result.Status,
                result.TotalDocuments,
                result.TotalSizeBytes,
                result.CompletedAt,
                result.ExpiresAt,
                result.ExternalReference
            ));
        })
        .WithName("GetUploadSession")
        .WithTags("Integration - Upload Sessions")
        .Produces<GetUploadSessionResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

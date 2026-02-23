using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public class CreateUploadSessionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/upload-sessions", async (
                CreateUploadSessionRequest request,
                HttpRequest httpRequest,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateUploadSessionCommand(
                    request.ClientReference,
                    request.ExternalCaseKey,
                    httpRequest.Headers.UserAgent.ToString(),
                    httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString()
                );

                var result = await sender.Send(command, cancellationToken);

                return Results.Ok(result);
            })
            .WithName("API - CreateUploadSession")
            .WithTags("Integration - Upload Sessions")
            .Produces<CreateUploadSessionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
using System.Security.Claims;
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
                ClaimsPrincipal user,
                HttpRequest httpRequest,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateUploadSessionCommand(
                    // Present on every OpenIddict access token; sub is a user id for user-bound tokens.
                    user.FindFirstValue("client_id"),
                    httpRequest.Headers.UserAgent.ToString(),
                    httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString()
                );

                var result = await sender.Send(command, cancellationToken);

                return Results.Ok(result);
            })
            .WithName("API - CreateUploadSession")
            .WithTags("Integration - Upload Sessions")
            .RequireAuthorization("Integration")
            .Produces<CreateUploadSessionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
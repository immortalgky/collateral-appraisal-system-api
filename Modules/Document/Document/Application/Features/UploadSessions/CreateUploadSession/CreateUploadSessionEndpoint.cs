using Microsoft.Extensions.Options;
using Shared.Configurations;

namespace Document.Domain.UploadSessions.Features.CreateUploadSession;

public record CreateUploadSessionResponse(Guid SessionId, DateTime ExpiresAt);

public class CreateUploadSessionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/documents/session", async (
            HttpRequest httpRequest,
            ISender sender,
            IOptions<FileStorageConfiguration> fileStorageOptions,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new CreateUploadSessionCommand(
                    httpRequest.Headers.UserAgent.ToString(),
                    httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);

            var expiresAt = DateTime.Now.AddHours(fileStorageOptions.Value.Cleanup.TempSessionExpirationHours);

            return Results.Ok(new CreateUploadSessionResponse(result.Id, expiresAt));
        })
        .WithName("CreateUploadSession")
        .WithTags("Documents")
        .Produces<CreateUploadSessionResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
        // .RequireAuthorization("CanUploadDocument");
    }
}
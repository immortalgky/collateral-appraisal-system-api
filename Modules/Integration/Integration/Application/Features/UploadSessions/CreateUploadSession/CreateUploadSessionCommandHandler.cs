using System.Globalization;
using Auth.Contracts.Clients;
using Document.Domain.UploadSessions.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Configurations;
using Shared.CQRS;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public class CreateUploadSessionCommandHandler(
    IRepository<UploadSession, Guid> uploadSessionRepository,
    IClientLookupService clientLookupService,
    IOptions<FileStorageConfiguration> fileStorageConfigurationOptions,
    ILogger<CreateUploadSessionCommandHandler> logger
) : ICommandHandler<CreateUploadSessionCommand, CreateUploadSessionResult>
{
    public async Task<CreateUploadSessionResult> Handle(
        CreateUploadSessionCommand command,
        CancellationToken cancellationToken)
    {
        var clientLifetime = string.IsNullOrWhiteSpace(command.ClientId)
            ? null
            : await clientLookupService.GetUploadSessionLifetimeAsync(command.ClientId, cancellationToken);
        var lifetime = clientLifetime
                       ?? TimeSpan.FromHours(fileStorageConfigurationOptions.Value.Cleanup.TempSessionExpirationHours);
        var expirationTime = DateTime.Now.Add(lifetime);

        logger.LogInformation(
            "Creating upload session for client {ClientId} with expiration at {ExpirationTime} ({LifetimeSource} lifetime). UserAgent: {UserAgent}, IP: {IpAddress}",
            command.ClientId,
            expirationTime,
            clientLifetime is null ? "default" : "client",
            command.UserAgent,
            command.IpAddress);

        var uploadSession = UploadSession.Create(
            expirationTime,
            command.UserAgent,
            command.IpAddress
        );

        await uploadSessionRepository.AddAsync(uploadSession, cancellationToken);

        logger.LogInformation(
            "Created upload session {SessionId} with expiration at {ExpirationTime}",
            uploadSession.Id,
            expirationTime);

        return new CreateUploadSessionResult(
            uploadSession.Id,
            uploadSession.Status,
            uploadSession.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            new UploadLimitation(
                fileStorageConfigurationOptions.Value.MaxFilesPerSession,
                fileStorageConfigurationOptions.Value.MaxFileSizeBytes,
                fileStorageConfigurationOptions.Value.MaxTotalSessionSizeBytes
            ));
    }
}
using System.Globalization;
using Document.Domain.UploadSessions.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Configurations;
using Shared.CQRS;

namespace Integration.Application.Features.UploadSessions.CreateUploadSession;

public class CreateUploadSessionCommandHandler(
    IRepository<UploadSession, Guid> uploadSessionRepository,
    IOptions<FileStorageConfiguration> fileStorageConfigurationOptions,
    ILogger<CreateUploadSessionCommandHandler> logger
) : ICommandHandler<CreateUploadSessionCommand, CreateUploadSessionResult>
{
    public async Task<CreateUploadSessionResult> Handle(
        CreateUploadSessionCommand command,
        CancellationToken cancellationToken)
    {
        var expirationTime =
            DateTime.Now.AddHours(fileStorageConfigurationOptions.Value.Cleanup.TempSessionExpirationHours);

        logger.LogInformation(
            "Creating upload session with expiration at {ExpirationTime}. UserAgent: {UserAgent}, IP: {IpAddress}",
            expirationTime,
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
            expirationTime.ToString("yyyy-MM-ddTHH:mm:ss"),
            new UploadLimitation(
                fileStorageConfigurationOptions.Value.MaxFilesPerSession,
                fileStorageConfigurationOptions.Value.MaxFileSizeBytes,
                fileStorageConfigurationOptions.Value.MaxTotalSessionSizeBytes
            ));
    }
}
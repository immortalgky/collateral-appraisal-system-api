using Document.Domain.UploadSessions.Model;
using Microsoft.Extensions.Options;
using Shared.Configurations;
using Shared.CQRS;
using Shared.Data;

namespace Document.Domain.UploadSessions.Features.CreateUploadSession;

public record CreateUploadSessionCommand(
    string? UserAgent,
    string? IpAddress
) : ICommand<CreateUploadSessionCommandResult>;

public record CreateUploadSessionCommandResult(Guid Id, DateTime ExpiresAt);

public class CreateUploadSessionCommandValidator : AbstractValidator<CreateUploadSessionCommand>
{
    public CreateUploadSessionCommandValidator()
    {
    }
}

public class CreateUploadSessionCommandHandler(
    IDocumentUnitOfWork uow,
    IOptions<FileStorageConfiguration> fileStorageOption,
    ILogger<CreateUploadSessionCommandHandler> logger
) : ICommandHandler<CreateUploadSessionCommand, CreateUploadSessionCommandResult>
{
    private readonly FileStorageConfiguration _fileStorageConfiguration = fileStorageOption.Value;
    private readonly IRepository<UploadSession, Guid> _repository = uow.Repository<UploadSession, Guid>();

    public async Task<CreateUploadSessionCommandResult> Handle(CreateUploadSessionCommand command,
        CancellationToken cancellationToken)
    {
        var expirationTime = DateTime.Now.AddHours(_fileStorageConfiguration.Cleanup.TempSessionExpirationHours);

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

        await _repository.AddAsync(uploadSession, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created upload session {SessionId} with expiration at {ExpirationTime}",
            uploadSession.Id,
            expirationTime);

        return new CreateUploadSessionCommandResult(uploadSession.Id, uploadSession.ExpiresAt);
    }
}
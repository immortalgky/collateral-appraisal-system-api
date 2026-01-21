using Document.Domain.UploadSessions.Model;
using Integration.Domain.IdempotencyRecords;
using Integration.Infrastructure.Repositories;
using Shared.CQRS;
using Shared.Data;
using System.Text.Json;

namespace Integration.Application.Features.UploadSessions.FinalizeUploadSession;

public class FinalizeUploadSessionCommandHandler(
    IRepository<UploadSession, Guid> uploadSessionRepository,
    IIdempotencyRecordRepository idempotencyRecordRepository
) : ICommandHandler<FinalizeUploadSessionCommand, FinalizeUploadSessionResult>
{
    public async Task<FinalizeUploadSessionResult> Handle(
        FinalizeUploadSessionCommand command,
        CancellationToken cancellationToken)
    {
        // Check idempotency
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existingRecord = await idempotencyRecordRepository.GetByKeyAsync(
                command.IdempotencyKey, cancellationToken);

            if (existingRecord is not null && !existingRecord.IsExpired())
            {
                if (!string.IsNullOrWhiteSpace(existingRecord.ResponseData))
                {
                    return JsonSerializer.Deserialize<FinalizeUploadSessionResult>(existingRecord.ResponseData)!;
                }
            }
        }

        var session = await uploadSessionRepository.GetByIdAsync(command.SessionId, cancellationToken);

        if (session is null)
        {
            throw new KeyNotFoundException($"Upload session {command.SessionId} not found");
        }

        if (session.Status == "Completed")
        {
            var documentIds = session.Documents.Select(d => d.Id).ToList();
            return new FinalizeUploadSessionResult(session.Id, session.Status, documentIds);
        }

        session.Complete(DateTime.UtcNow);
        await uploadSessionRepository.SaveChangesAsync(cancellationToken);

        var result = new FinalizeUploadSessionResult(
            session.Id,
            session.Status,
            session.Documents.Select(d => d.Id).ToList()
        );

        // Store idempotency record
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var idempotencyRecord = IdempotencyRecord.Create(
                command.IdempotencyKey,
                "FinalizeUploadSession"
            );
            idempotencyRecord.SetResponse(JsonSerializer.Serialize(result), 200);
            await idempotencyRecordRepository.AddAsync(idempotencyRecord, cancellationToken);
            await idempotencyRecordRepository.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}

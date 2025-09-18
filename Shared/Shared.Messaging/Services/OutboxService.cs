using MassTransit;
using Microsoft.Extensions.Configuration;
using Shared.Messaging.Extensions;
using Shared.OutboxPatterns.Repository;
using Shared.OutboxPatterns.Services;

namespace Shared.Messaging.Services;

public class OutboxService : IOutboxService
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfiguration _configuration;
    private readonly IOutboxReadRepository _readRepository;
    private readonly IOutboxRepository _repository;
    private readonly string _schema;
    private readonly short _chunk;

    public OutboxService(
        IPublishEndpoint publishEndpoint,
        IConfiguration configuration,
        IOutboxReadRepository readRepository,
        IOutboxRepository repository,
        string schema)
    {
        _publishEndpoint = publishEndpoint;
        _configuration = configuration;
        _chunk = _configuration.GetValue<short>("OutboxConfigurations:Chunk");
        _readRepository = readRepository;
        _repository = repository;
        _schema = schema;
    }
    public async Task<short> PublishEvent(CancellationToken cancellationToken = default)
    {
        var messages = await _readRepository.GetAllAsync(_schema, cancellationToken);

        if (messages.Count == 0) return 0;

        await MessageCyclesAsync(messages, cancellationToken);

        return (short)messages.Count;
    }

    private async Task MessageCyclesAsync(List<OutboxPatterns.Models.OutboxMessage>? messages, CancellationToken cancellationToken)
    {
        if (messages is null) return;

        foreach (var chunk in messages.Chunk(_chunk))
        {
            // ✅ Transaction per chunk for optimal balance
            using var transaction = await _repository.BeginTransaction(cancellationToken);
            
            try
            {
                foreach (var message in chunk)
                {
                    try
                    {
                        await _publishEndpoint.PublishDeserializedEvent(
                            message.Payload,
                            message.EventType,
                            cancellationToken
                        );
                        await _repository.DeleteAsync(message.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        var isInfraFailure = OutboxPatterns.Models.OutboxMessage.ShouldTreatAsInfrastructureFailure(ex);

                        if (message.ShouldRetry())
                        {
                            message.IncrementRetry(ex.Message, isInfraFailure);
                            await _repository.UpdateAsync(message);
                        }
                        else
                        {
                            await _repository.DeleteAsync(message.Id, cancellationToken);
                        }
                    }
                }

                await _repository.SaveChangeAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
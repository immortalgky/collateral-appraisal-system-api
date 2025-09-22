using MassTransit;
using Microsoft.Extensions.Configuration;
using Shared.Messaging.Extensions;
using Shared.OutboxPatterns.Repository;
using Shared.OutboxPatterns.Services;

namespace Shared.Messaging.Services;

public class OutboxService(
    IPublishEndpoint _publishEndpoint,
    IConfiguration _configuration,
    IOutboxReadRepository _readRepository,
    IOutboxRepository _repository,
    string schema
) : IOutboxService
{
    private readonly string _schema = schema;
    private readonly short _chunk = _configuration.GetValue<short>("OutboxConfigurations:Chunk");
    private int _messages = 0;

    public async Task<int> PublishEvent(CancellationToken cancellationToken = default)
    {
        using var transaction = await _repository.BeginTransaction(cancellationToken);

        try
        {
            var messages = await _readRepository.GetAllAsync(_schema, cancellationToken);

            _messages = messages.Count();

            if (messages.Count == 0) return 0;

            await MessageCyclesAsync(messages, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await transaction.CommitAsync(cancellationToken);

        return _messages;
    }

    private async Task MessageCyclesAsync(List<OutboxPatterns.Models.OutboxMessage>? messages, CancellationToken cancellationToken)
    {
        if (messages is null) return;

        foreach (var chunk in messages.Chunk(_chunk))
        {
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
                            await _repository.UpdateAsync(message, cancellationToken);
                        }
                        else
                        {
                            await _repository.DeleteAsync(message.Id, cancellationToken);
                        }
                    }
                }

                await _repository.SaveChangeAsync(cancellationToken);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
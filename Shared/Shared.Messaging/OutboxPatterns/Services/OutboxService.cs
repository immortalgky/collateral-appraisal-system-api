namespace Shared.Messaging.OutboxPatterns.Services;

public class OutboxService(
    IPublishEndpoint _publishEndpoint,
    IConfiguration _configuration,
    IOutboxReadRepository _readRepository,
    IOutboxRepository _repository,
    ILogger<OutboxService> _logger
) : IOutboxService
{
private readonly short _chunk = _configuration.GetValue<short>("Jobs:OutboxProcessor:Chunk");


    public async Task PublishEvent(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📦 Service Called");
        while (!cancellationToken.IsCancellationRequested)
        {
            var transaction = await _repository.BeginTransaction(cancellationToken);

            try
            {
                var messages = await _readRepository.GetMessageAsync(cancellationToken);

                if (messages is null || messages.Count == 0)
                {
                    await transaction.CommitAsync(cancellationToken);

                    _logger.LogInformation("📦 No Message");

                    return;
                }

                _logger.LogInformation("📦 {message} Message", messages.Count);

                await MessageCyclesAsync(messages, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);

                _logger.LogInformation("📦 Boom! {boom}", ex.Message);

                return;
            }
        }
    }

    private async Task MessageCyclesAsync(List<OutboxMessage>? messages, CancellationToken cancellationToken)
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
                            message.Id,
                            message.Payload,
                            message.EventType,
                            cancellationToken
                        );
                        _logger.LogInformation("📦 Publish messages!");

                        await _repository.DeleteAsync(message.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        message.Update(ex.Message);
                        message.IncrementRetry();
                    }
                }

                await _repository.SaveChangeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Boom! Message processing failed.", ex);
            }
        }
    }
}
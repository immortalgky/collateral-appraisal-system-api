namespace Shared.Messaging.OutboxPatterns.Services;

public class OutboxService(
    IPublishEndpoint _publishEndpoint,
    IConfiguration _configuration,
    IOutboxReadRepository _readRepository,
    IOutboxRepository _repository,
    string schema
) : IOutboxService
{
    private readonly string _schema = schema;
    private readonly short _chunk = _configuration.GetValue<short>("Jobs:OutboxProcessor:Chunk");
    private int _messages = 0;


    public async Task<int> PublishEvent(CancellationToken cancellationToken = default)
    {
        do
        {
            using var transaction = await _repository.BeginTransaction(cancellationToken);

            try
            {
                var messages = await _readRepository.GetMessageAsync(_schema, cancellationToken);

                _messages = messages.Count();

                if (messages.Count == 0)
                {
                    await transaction.CommitAsync(cancellationToken);
                    
                    return 0;
                }

                await MessageCyclesAsync(messages, cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            await transaction.CommitAsync(cancellationToken);
        }
        while (_messages > 0);

        return _messages;
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
                            message.Payload,
                            message.EventType,
                            cancellationToken
                        );
                        await _repository.DeleteAsync(message.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        var isInfraFailure = OutboxMessage.ShouldTreatAsInfrastructureFailure(ex);


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
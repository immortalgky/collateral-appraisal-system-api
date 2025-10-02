namespace Shared.Messaging.OutboxPatterns.Services;

public class InboxService(
    IConfiguration _configuration,
    IInboxReadRepository _readRepository,
    IInboxRepository _repository,
    ILogger<InboxService> _logger) : IInboxService
{    public async Task<bool> CheckDuplicate(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _readRepository.GetByIdAsync(id, cancellationToken);

        if (message is not null)
        {
            return true;
        }
        return false;
    }

    public async Task ClearTimeOutMessage(CancellationToken cancellationToken = default)
    {
        var timeoutDate = DateTime.UtcNow.AddDays(_configuration.GetValue<int>("Jobs:RetentionDays"));

        var messages = await _readRepository.GetAllAsync(cancellationToken);

        foreach (var message in messages)
        {
            if (message.ReceiveAt < timeoutDate)
                await _repository.DeleteAsync(message.Id, cancellationToken);
        }
    }

    public async Task<bool> AddMessageInboxAsync<TMessage>(ConsumeContext<TMessage> context, CancellationToken cancellationToken = default) where TMessage : class
    {
        var dup = await CheckDuplicate(context.MessageId.Value, cancellationToken);

        if (dup)
        {
            _logger.LogInformation("📦 Is Dup!!! ");
            return false;
        }

        var message = InboxMessage.Create(
            context.MessageId.Value,
            DateTime.UtcNow,
            typeof(TMessage).Name,
            JsonSerializer.Serialize(context.Message));

        await _repository.AddAsync(message, cancellationToken);

        await _repository.SaveChangeAsync(cancellationToken);
        
        return true;
    }

}
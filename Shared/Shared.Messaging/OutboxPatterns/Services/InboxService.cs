using Microsoft.Extensions.Configuration;
using Shared.Data.Models;
using Shared.Exceptions;
using Shared.Messaging.OutboxPatterns.Repository;

namespace Shared.Messaging.OutboxPatterns.Services;

public class InboxService(
    IConfiguration _configuration,
    IInboxReadRepository _readRepository,
    IInboxRepository _repository) : IInboxService
{    public async Task CheckDuplicate(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _readRepository.GetMessageByIdAsync(id, cancellationToken);

        if (message is null)throw new NotFoundException("This Event Is Duplicate");
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

    public async Task AddMessageInboxAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = typeof(TMessage);
        var eventId = messageType.GetProperty("EventId")?.GetValue(message) as Guid?;
        var occurredOn = messageType.GetProperty("OccurredOn")?.GetValue(message) as DateTime?;

        if (!eventId.HasValue || !occurredOn.HasValue)
            throw new ArgumentException("Message must contain 'EventId' and 'OccurredOn' properties.", nameof(message));

        var inboxMessage = InboxMessage.Create(
            eventId.Value,
            occurredOn.Value,
            messageType.FullName!,
            System.Text.Json.JsonSerializer.Serialize(message)
        );

        await _repository.AddAsync(inboxMessage, cancellationToken);
        
        await _repository.SaveChangAsync(cancellationToken);
    }

}
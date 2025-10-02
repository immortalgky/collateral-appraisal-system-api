namespace Shared.Messaging.OutboxPatterns.Wrappers;

// Per-consumer wrapper so each consumer gets its own runtime type that MassTransit can register
public class ConsumeWrapper<TMessage, TConsumer> : IConsumer<TMessage>
    where TMessage : class
    where TConsumer : IConsumer<TMessage>
{
    private readonly TConsumer _innerConsumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _moduleSchema;
    private readonly ILogger _logger;

    public ConsumeWrapper(TConsumer innerConsumer, IServiceProvider serviceProvider, string moduleSchema, ILogger logger)
    {
        _innerConsumer = innerConsumer ?? throw new ArgumentNullException(nameof(innerConsumer));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _moduleSchema = moduleSchema ?? throw new ArgumentNullException(nameof(moduleSchema));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<TMessage> context)
    {
        try
        {
            _logger.LogDebug("ConsumeWrapper's {CosumeType}", context.GetType());

            using var scope = _serviceProvider.CreateScope();

            var inboxService = scope.ServiceProvider.GetKeyedService<IInboxService>(_moduleSchema);

            if (inboxService == null)
            {
                 _logger.LogDebug("Get Service Fail Processed Nomal consume");

                await _innerConsumer.Consume(context);

                return;
            }
            
            if (context.MessageId == null)
            {
                _logger.LogWarning("MessageId is null, cannot use inbox pattern for message type {MessageType}", typeof(TMessage).Name);

                await _innerConsumer.Consume(context);

                return;
            }

            var eventId = context.MessageId;

            _logger.LogDebug("Add Event Before Processing and Check Duplicate: {EventId}", eventId.Value);

            var isDuplicate = await inboxService.AddMessageInboxAsync(context, context.CancellationToken);
            
            if (!isDuplicate)
            {
                _logger.LogDebug("Duplicate message detected with EventId: {EventId}. Skipping processing.", eventId.Value);
                return; //
            }

            _logger.LogDebug("Message added to inbox for processing with EventId: {EventId}", eventId.Value);

            await _innerConsumer.Consume(context); // Inner consume (Real)
            
            _logger.LogDebug("Successfully processed message of type {MessageType} with ID: {MessageId}", typeof(TMessage).Name, context.MessageId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while consuming the message of type {MessageType} with MessageId: {MessageId} for module: {ModuleSchema}", 
                typeof(TMessage).Name, context.MessageId, _moduleSchema);
        }
    }
}
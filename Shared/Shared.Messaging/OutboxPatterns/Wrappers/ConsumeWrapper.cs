using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Messaging.OutboxPatterns.Services;

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
            using var scope = _serviceProvider.CreateScope();

            var inboxService = scope.ServiceProvider.GetKeyedService<IInboxService>(_moduleSchema);

            if (inboxService == null || context.MessageId == null) return;

            await inboxService.CheckDuplicate(context.MessageId.Value, context.CancellationToken);

            await inboxService.AddMessageInboxAsync(context.Message);

            await _innerConsumer.Consume(context); // Inner consume (Real)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while consuming the message of type {MessageType}", typeof(TMessage).Name);
        }
    }
}

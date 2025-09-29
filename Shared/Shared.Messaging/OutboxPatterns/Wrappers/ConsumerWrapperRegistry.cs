using System.Collections.Concurrent;

namespace Shared.Messaging.OutboxPatterns.Wrappers;

internal static class ConsumerWrapperRegistry
{
    private static readonly ConcurrentBag<Type> _types = new();
    private static readonly ConcurrentDictionary<Type, byte> _wrappedConsumers = new();

    public static void Register(Type consumerWrapperType)
    {
        if (consumerWrapperType == null) throw new ArgumentNullException(nameof(consumerWrapperType));

        _types.Add(consumerWrapperType);

        if (consumerWrapperType.IsGenericType &&
            consumerWrapperType.GetGenericTypeDefinition() == typeof(ConsumeWrapper<,>))
        {
            var innerConsumerType = consumerWrapperType.GetGenericArguments()[1];
            _wrappedConsumers.TryAdd(innerConsumerType, 0);
        }
    }

    public static IReadOnlyCollection<Type> GetRegistered() => _types.ToArray();

    public static IReadOnlyCollection<Type> GetWrappedConsumers() => _wrappedConsumers.Keys.ToArray();
}

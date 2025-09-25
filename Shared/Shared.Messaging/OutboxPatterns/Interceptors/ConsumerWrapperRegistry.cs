using System.Collections.Concurrent;

namespace Shared.Messaging.OutboxPatterns.Interceptors;

internal static class ConsumerWrapperRegistry
{
    private static readonly ConcurrentBag<Type> _types = new();

    public static void Register(Type consumerWrapperType)
    {
        if (consumerWrapperType == null) throw new ArgumentNullException(nameof(consumerWrapperType));
        _types.Add(consumerWrapperType);
    }

    public static IReadOnlyCollection<Type> GetRegistered() => _types.ToArray();
}

namespace Notification.Contracts.Realtime;

public interface IRealtimeNotifier
{
    Task SendToGroupAsync(string groupName, string eventName, object payload, CancellationToken ct = default);
}

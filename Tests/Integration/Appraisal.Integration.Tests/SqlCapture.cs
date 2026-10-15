using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Records the SQL EF sends while it is alive, via EF's DiagnosticListener. The listener is
/// process-wide, so a command is recorded only when it runs in the async flow that created this
/// capture (<see cref="Current"/>) — hosted services (outbox, Hangfire, SLA jobs) have their own
/// flows and can't pollute exact-count assertions.
/// </summary>
internal sealed class SqlCapture : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private static readonly AsyncLocal<SqlCapture?> Current = new();

    public ConcurrentQueue<string> Commands { get; } = new();
    private readonly List<IDisposable> _subscriptions = [];

    public SqlCapture()
    {
        Current.Value = this;
        lock (_subscriptions) _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
    }

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == DbLoggerCategory.Name)
            lock (_subscriptions) _subscriptions.Add(listener.Subscribe(this));
    }

    public void OnNext(KeyValuePair<string, object?> evt)
    {
        if (evt.Key == RelationalEventId.CommandExecuting.Name && evt.Value is CommandEventData data
            && ReferenceEquals(Current.Value, this))
            Commands.Enqueue(data.Command.CommandText);
    }

    public void OnCompleted() { }
    public void OnError(Exception error) { }

    public void Dispose()
    {
        Current.Value = null;
        lock (_subscriptions) _subscriptions.ForEach(s => s.Dispose());
    }
}

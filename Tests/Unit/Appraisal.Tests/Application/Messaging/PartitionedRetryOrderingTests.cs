using System.Collections.Concurrent;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Appraisal.Tests.Application.Messaging;

/// <summary>
/// Pins the MassTransit behaviour Bootstrapper/Api/Program.cs relies on for the partitioned appraisal
/// endpoints (appraisal-sync, appraisal-ext-cycle, appraisal-status-dashboard): an endpoint-level retry runs
/// INSIDE the partition, so a failed message keeps it and the next message for the same key waits, and it
/// does not stack with the bus-level retry. With only the bus retry, B overtakes a retrying A.
/// This builds its own bus, so it guards the framework, not Program.cs's wiring.
/// </summary>
public class PartitionedRetryOrderingTests
{
    private const string Queue = "partitioned-retry-probe";

    [Fact]
    public async Task EndpointLevelRetry_HoldsThePartition_AndDoesNotStackWithTheBusRetry()
    {
        var probe = new Probe();
        var services = new ServiceCollection();
        services.AddSingleton(probe);
        services.AddMassTransit(x =>
        {
            x.AddConsumer<ProbeConsumer>();
            x.AddConsumer<FaultProbeConsumer>();
            x.UsingInMemory((context, cfg) =>
            {
                // 1s so that, without the fix, B's window to overtake A dwarfs any scheduling delay on a slow runner.
                cfg.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(1)));
                cfg.ReceiveEndpoint(Queue, e =>
                {
                    // Production gets an effective concurrency of 16 from PrefetchCount = 16 on RabbitMQ. Left to
                    // the in-memory default, a 1-CPU runner processes one message at a time, B could never overtake
                    // A anyway, and the ordering assertion would prove nothing.
                    e.ConcurrentMessageLimit = 16;
                    var partitioner = e.CreatePartitioner(16);
                    e.UseMessageRetry(r => r.Intervals(TimeSpan.FromMilliseconds(300),
                        TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50)));
                    e.ConfigureConsumer<ProbeConsumer>(context);
                    e.UsePartitioner<ProbeMessage>(partitioner, m => m.Message.Key);
                });
                cfg.ReceiveEndpoint($"{Queue}-faults", e => e.ConfigureConsumer<FaultProbeConsumer>(context));
            });
        });

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IBusControl>();
        var ct = TestContext.Current.CancellationToken;
        int cAttemptsAtFault;
        await bus.StartAsync(ct);
        try
        {
            var endpoint = await bus.GetSendEndpoint(new Uri($"queue:{Queue}"));
            var key = Guid.NewGuid();

            // B (same key) is sent only once A's first attempt has failed, i.e. while A waits for its retry.
            await endpoint.Send(new ProbeMessage(key, "A", FailTimes: 1), ct);
            await probe.AFailedOnce.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            await endpoint.Send(new ProbeMessage(key, "B", FailTimes: 0), ct);
            await endpoint.Send(new ProbeMessage(Guid.NewGuid(), "C", FailTimes: int.MaxValue), ct);

            await probe.BothDone.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

            // The fault is published only once every retry layer has given up, so the attempt count taken then is
            // final: a stacked bus retry (~24 attempts, ~7s at these intervals) shows up as more attempts.
            cAttemptsAtFault = await probe.CAttemptsAtFault.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }

        Assert.Equal(["A", "B"], probe.Completed.ToArray());
        Assert.Equal(4, cAttemptsAtFault); // 1 + 3 endpoint-level retries; the bus retry adds none
    }

    public record ProbeMessage(Guid Key, string Name, int FailTimes);

    public class Probe
    {
        public ConcurrentQueue<string> Completed { get; } = new();
        public TaskCompletionSource AFailedOnce { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BothDone { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> CAttemptsAtFault { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<string, int> _attempts = new();
        public int Attempt(string name) => _attempts.AddOrUpdate(name, 1, (_, n) => n + 1);
        public int AttemptsOf(string name) => _attempts.GetValueOrDefault(name);
    }

    public class ProbeConsumer(Probe probe) : IConsumer<ProbeMessage>
    {
        public Task Consume(ConsumeContext<ProbeMessage> context)
        {
            var m = context.Message;
            if (probe.Attempt(m.Name) <= m.FailTimes)
            {
                if (m.Name == "A") probe.AFailedOnce.TrySetResult();
                throw new InvalidOperationException($"{m.Name} fails on purpose");
            }

            probe.Completed.Enqueue(m.Name);
            if (probe.Completed.Count == 2) probe.BothDone.TrySetResult();
            return Task.CompletedTask;
        }
    }

    public class FaultProbeConsumer(Probe probe) : IConsumer<Fault<ProbeMessage>>
    {
        public Task Consume(ConsumeContext<Fault<ProbeMessage>> context)
        {
            if (context.Message.Message.Name == "C")
                probe.CAttemptsAtFault.TrySetResult(probe.AttemptsOf("C"));
            return Task.CompletedTask;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Shared.Messaging.Services;

/// <summary>
/// Base class for background services that must run on at most one instance at a time
/// across multiple servers. Uses the shared <c>BackgroundServiceLease</c> table (one row per
/// <see cref="LockId"/>) as a database-backed lease — the same primitive used by
/// <see cref="IntegrationEventDeliveryService{TDbContext}"/>.
///
/// Each instance attempts to acquire/renew the lease at the start of every work tick.
/// Only the holder runs <see cref="ExecuteWhileLeasedAsync"/>; standby instances poll
/// the lease until it expires (typically when the holder crashes).
///
/// IMPORTANT: pick <see cref="LeaseDuration"/> ≥ 3 × (<see cref="WorkInterval"/> + worst-case
/// tick runtime). The lease is renewed only between ticks, so a GC pause or slow tick that
/// overruns the lease will let a second instance steal it mid-work.
/// </summary>
public abstract class LeasedBackgroundService<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    protected LeasedBackgroundService(IServiceScopeFactory scopeFactory, ILogger logger, IDateTimeProvider dateTimeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Unique key in <c>BackgroundServiceLease</c> identifying this service. Must be distinct
    /// from every other lease holder sharing the same DbContext (including
    /// <see cref="IntegrationEventDeliveryService{TDbContext}"/>, which uses
    /// <c>typeof(TDbContext).Name</c>). Convention: <c>"{DbContextName}-{ServiceShortName}"</c>.
    /// </summary>
    protected abstract string LockId { get; }

    /// <summary>How long the lease is held before another instance can claim an expired one.</summary>
    protected virtual TimeSpan LeaseDuration => TimeSpan.FromSeconds(180);

    /// <summary>Interval between successive ticks when this instance IS the leader.</summary>
    protected virtual TimeSpan WorkInterval => TimeSpan.FromSeconds(60);

    /// <summary>Interval between lease-acquire attempts when this instance is NOT the leader.</summary>
    protected virtual TimeSpan StandbyPollInterval => TimeSpan.FromSeconds(5);

    /// <summary>
    /// One tick of work. Invoked only while this instance holds the lease. The provided
    /// scope is the same scope used to acquire the lease — implementations should
    /// <c>scope.ServiceProvider.GetRequiredService&lt;...&gt;</c> for any dependencies.
    /// The scope is disposed when this method returns; the loop then sleeps before
    /// creating a fresh scope for the next tick.
    /// </summary>
    protected abstract Task ExecuteWhileLeasedAsync(IServiceScope scope, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[LEASE] {LockId} starting (instance {InstanceId})", LockId, _instanceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            var acquired = false;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                    acquired = await TryAcquireLeaseAsync(dbContext, stoppingToken);
                    if (acquired)
                        await ExecuteWhileLeasedAsync(scope, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LEASE] {LockId} tick failed", LockId);
            }

            try
            {
                await Task.Delay(acquired ? WorkInterval : StandbyPollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            await ReleaseLeaseAsync(dbContext, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LEASE] {LockId} failed to release lease on shutdown", LockId);
        }

        _logger.LogInformation("[LEASE] {LockId} stopped", LockId);
    }

    private async Task<bool> TryAcquireLeaseAsync(TDbContext dbContext, CancellationToken ct)
    {
        var now = _dateTimeProvider.ApplicationNow;
        var leasedUntil = now.Add(LeaseDuration);
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";

        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[BackgroundServiceLease] " +
            "SET InstanceId = {0}, LeasedUntil = {1}, AcquiredAt = {2} " +
            "WHERE Id = {3} AND (InstanceId = {0} OR LeasedUntil < {2})",
            new object[] { _instanceId, leasedUntil, now, LockId }, ct);

        if (rowsAffected > 0)
            return true;

        try
        {
            var inserted = await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO [" + schema + "].[BackgroundServiceLease] (Id, InstanceId, LeasedUntil, AcquiredAt) " +
                "SELECT {0}, {1}, {2}, {3} " +
                "WHERE NOT EXISTS (SELECT 1 FROM [" + schema + "].[BackgroundServiceLease] WHERE Id = {0})",
                new object[] { LockId, _instanceId, leasedUntil, now }, ct);

            return inserted > 0;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private async Task ReleaseLeaseAsync(TDbContext dbContext, CancellationToken ct)
    {
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";

        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[BackgroundServiceLease] " +
            "SET LeasedUntil = {0} " +
            "WHERE Id = {1} AND InstanceId = {2}",
            new object[] { _dateTimeProvider.ApplicationNow, LockId, _instanceId }, ct);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Scheduling;

/// <summary>
/// One administrable recurring job: its code-side definition plus module-scoped accessors for the
/// <c>{schema}.JobSchedules</c> row that overrides it.
///
/// <see cref="JobSchedule"/> is mapped once per module schema, so there is no single DbContext that
/// can reach every row. The accessors below close over the concrete <c>TContext</c> inside
/// <see cref="RecurringJobScheduleExtensions.UseModuleRecurringJobs{TContext}"/> — the only place
/// that type is known — which lets an admin endpoint in any module read and write all of them.
/// </summary>
public sealed class JobScheduleRegistration
{
    public required string JobId { get; init; }

    /// <summary>Owning module, derived from the DbContext name (e.g. "Appraisal").</summary>
    public required string Module { get; init; }

    public required RecurringJobDefinition Definition { get; init; }

    /// <summary>Reads the persisted override, or null when the row is missing.</summary>
    public required Func<IServiceProvider, CancellationToken, Task<JobSchedule?>> LoadAsync { get; init; }

    /// <summary>
    /// Applies <c>mutate</c> to the tracked row and saves. Returns the updated row, or null when
    /// there is no row to update.
    /// </summary>
    public required Func<IServiceProvider, Action<JobSchedule>, CancellationToken, Task<JobSchedule?>>
        UpdateAsync { get; init; }
}

/// <summary>
/// Cross-module catalog of recurring jobs, populated at startup as each module registers its jobs.
/// Registered as a singleton; it is written only during startup and read by the admin endpoints.
/// </summary>
public interface IJobScheduleRegistry
{
    IReadOnlyList<JobScheduleRegistration> All { get; }

    JobScheduleRegistration? Find(string jobId);

    void Add(JobScheduleRegistration registration);
}

public sealed class JobScheduleRegistry : IJobScheduleRegistry
{
    // Modules register on the startup thread, but UseXModule() ordering is not something this class
    // controls, so keep the writes guarded rather than assuming single-threaded population.
    private readonly Dictionary<string, JobScheduleRegistration> _byJobId = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public IReadOnlyList<JobScheduleRegistration> All
    {
        get
        {
            lock (_gate)
            {
                return _byJobId.Values
                    .OrderBy(r => r.Module, StringComparer.Ordinal)
                    .ThenBy(r => r.JobId, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    public JobScheduleRegistration? Find(string jobId)
    {
        lock (_gate)
        {
            return _byJobId.GetValueOrDefault(jobId);
        }
    }

    public void Add(JobScheduleRegistration registration)
    {
        lock (_gate)
        {
            _byJobId[registration.JobId] = registration;
        }
    }
}

public static class JobScheduleRegistryExtensions
{
    /// <summary>
    /// Must be called before the first <c>UseModuleRecurringJobs</c>; jobs registered while the
    /// singleton is absent are simply not administrable.
    /// </summary>
    public static IServiceCollection AddJobScheduleRegistry(this IServiceCollection services)
    {
        services.AddSingleton<IJobScheduleRegistry, JobScheduleRegistry>();
        return services;
    }

    /// <summary>"AppraisalDbContext" → "Appraisal"; used as the display/group name.</summary>
    internal static string ToModuleName(this Type contextType)
    {
        var name = contextType.Name;
        return name.EndsWith("DbContext", StringComparison.Ordinal)
            ? name[..^"DbContext".Length]
            : name;
    }

    internal static JobScheduleRegistration BuildRegistration<TContext>(
        RecurringJobDefinition definition)
        where TContext : DbContext
    {
        return new JobScheduleRegistration
        {
            JobId = definition.JobId,
            Module = typeof(TContext).ToModuleName(),
            Definition = definition,
            LoadAsync = async (services, ct) =>
            {
                var db = services.GetRequiredService<TContext>();
                return await db.Set<JobSchedule>().AsNoTracking()
                    .FirstOrDefaultAsync(s => s.JobId == definition.JobId, ct);
            },
            UpdateAsync = async (services, mutate, ct) =>
            {
                var db = services.GetRequiredService<TContext>();
                var row = await db.Set<JobSchedule>()
                    .FirstOrDefaultAsync(s => s.JobId == definition.JobId, ct);
                if (row is null) return null;

                mutate(row);
                await db.SaveChangesAsync(ct);
                return row;
            },
        };
    }
}

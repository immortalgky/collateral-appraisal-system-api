using Microsoft.EntityFrameworkCore;

namespace Shared.Scheduling;

public static class JobScheduleModelExtensions
{
    /// <summary>
    /// Adds the <see cref="JobSchedule"/> table to the calling DbContext's default schema.
    /// Call from <c>OnModelCreating</c> in any module that owns recurring jobs, then register those
    /// jobs with <c>app.UseModuleRecurringJobs&lt;TContext&gt;(...)</c>. Mirrors
    /// <c>AddIntegrationEventInbox()/AddIntegrationEventOutbox()</c>.
    /// </summary>
    public static ModelBuilder AddJobSchedules(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new JobScheduleConfiguration());
        return modelBuilder;
    }
}

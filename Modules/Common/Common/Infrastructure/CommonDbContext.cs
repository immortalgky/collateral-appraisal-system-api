using System.Reflection;
using Common.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Outbox;

namespace Common.Infrastructure;

public class CommonDbContext(DbContextOptions<CommonDbContext> options) : DbContext(options)
{
    public DbSet<DailyAppraisalCount> DailyAppraisalCounts => Set<DailyAppraisalCount>();
    public DbSet<RequestStatusSummary> RequestStatusSummaries => Set<RequestStatusSummary>();
    public DbSet<TeamWorkloadSummary> TeamWorkloadSummaries => Set<TeamWorkloadSummary>();
    public DbSet<CompanyAppraisalSummary> CompanyAppraisalSummaries => Set<CompanyAppraisalSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("common");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.AddIntegrationEventInbox();

        base.OnModelCreating(modelBuilder);
    }
}

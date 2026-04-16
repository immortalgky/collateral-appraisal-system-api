using System.Reflection;
using Common.Domain.Notes;
using Common.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Outbox;

namespace Common.Infrastructure;

public class CommonDbContext(DbContextOptions<CommonDbContext> options) : DbContext(options)
{
    public DbSet<DailyAppraisalCount> DailyAppraisalCounts => Set<DailyAppraisalCount>();
    public DbSet<AppraisalStatusSummary> AppraisalStatusSummaries => Set<AppraisalStatusSummary>();
    public DbSet<CompanyAppraisalSummary> CompanyAppraisalSummaries => Set<CompanyAppraisalSummary>();
    public DbSet<DashboardNote> DashboardNotes => Set<DashboardNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("common");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.AddIntegrationEventInbox();

        base.OnModelCreating(modelBuilder);
    }
}

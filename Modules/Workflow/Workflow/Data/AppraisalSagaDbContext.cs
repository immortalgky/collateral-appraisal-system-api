using Workflow.Data.Configurations;
using MassTransit.EntityFrameworkCoreIntegration;

namespace Workflow.Data;

public class AppraisalSagaDbContext(DbContextOptions<AppraisalSagaDbContext> options) : SagaDbContext(options)
{
    protected override IEnumerable<ISagaClassMap> Configurations =>
        [new AppraisalSagaStateConfiguration()];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("saga");

        base.OnModelCreating(modelBuilder);
    }
}
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Assignment.Data.Configurations;

public class AppraisalSagaStateConfiguration : SagaClassMap<AppraisalSagaState>
{
    protected override void Configure(EntityTypeBuilder<AppraisalSagaState> entity, ModelBuilder model)
    {
        entity.Property(x => x.CurrentState)
            .HasMaxLength(100);

        entity.Property(x => x.Assignee)
            .HasMaxLength(1000);

        entity.Property(x => x.AssignType)
            .HasMaxLength(1);
    }
}
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Domain.Committees;

namespace Workflow.Data.Configurations;

public class CommitteeApprovalConditionConfiguration : IEntityTypeConfiguration<CommitteeApprovalCondition>
{
    public void Configure(EntityTypeBuilder<CommitteeApprovalCondition> builder)
    {
        builder.ToTable("CommitteeApprovalConditions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ConditionType).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.RoleRequired).HasMaxLength(50);
        builder.Property(c => c.Description).HasMaxLength(500);

        builder.HasIndex(c => c.CommitteeId);
    }
}

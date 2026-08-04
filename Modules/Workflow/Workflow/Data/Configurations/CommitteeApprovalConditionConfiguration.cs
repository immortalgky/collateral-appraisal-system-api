using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Domain.Committees;

namespace Workflow.Data.Configurations;

public class CommitteeApprovalConditionConfiguration : IEntityTypeConfiguration<CommitteeApprovalCondition>
{
    public void Configure(EntityTypeBuilder<CommitteeApprovalCondition> builder)
    {
        builder.ToTable("CommitteeApprovalConditions");
        builder.HasKey(c => c.Id);

        // The key is assigned by CommitteeApprovalCondition.Create, not generated. Without this,
        // EF's Guid-key convention is ValueGeneratedOnAdd, so a child reached through the parent's
        // navigation with a non-empty key is taken to be an EXISTING row — SaveChanges then issues
        // an UPDATE that matches nothing and throws DbUpdateConcurrencyException.
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.ConditionType).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.RoleRequired).HasMaxLength(50);
        builder.Property(c => c.Description).HasMaxLength(500);

        builder.HasIndex(c => c.CommitteeId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Data.Entities;

namespace Workflow.Data.Configurations;

public class AutoAssignmentRuleConfiguration : IEntityTypeConfiguration<AutoAssignmentRule>
{
    public void Configure(EntityTypeBuilder<AutoAssignmentRule> builder)
    {
        builder.ToTable("AutoAssignmentRules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RuleName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Priority)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.Channels).HasMaxLength(500);
        builder.Property(x => x.EntrySources).HasMaxLength(200);
        builder.Property(x => x.LoanTypes).HasMaxLength(500);
        builder.Property(x => x.Priorities).HasMaxLength(200);

        builder.Property(x => x.MinFacilityLimit).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaxFacilityLimit).HasColumnType("decimal(18,2)");

        builder.Property(x => x.ConditionExpression)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.RoutingDecision)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.UpdatedBy)
            .IsRequired()
            .HasMaxLength(100);

        // Evaluation order: active rules by ascending priority. Filtered to active rows because
        // that is the only set RoutingActivity ever reads.
        builder.HasIndex(x => new { x.IsActive, x.Priority })
            .HasDatabaseName("IX_AutoAssignmentRules_Active_Priority")
            .HasFilter("[IsActive] = 1");
    }
}

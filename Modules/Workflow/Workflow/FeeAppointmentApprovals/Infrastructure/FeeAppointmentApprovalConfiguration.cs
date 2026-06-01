using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.FeeAppointmentApprovals.Domain;

namespace Workflow.FeeAppointmentApprovals.Infrastructure;

public class FeeAppointmentApprovalConfiguration : IEntityTypeConfiguration<FeeAppointmentApproval>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<FeeAppointmentApproval> builder)
    {
        builder.ToTable("FeeAppointmentApprovals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AppraisalId).IsRequired();

        builder.Property(x => x.RequestSource)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.ResolvedTier)
            .HasMaxLength(100);

        builder.Property(x => x.ApproverAssignee)
            .HasMaxLength(100);

        builder.Property(x => x.AssignedType)
            .HasMaxLength(5);

        builder.Property(x => x.FollowupWorkflowInstanceId);

        builder.Property(x => x.CancellationReason)
            .HasMaxLength(1000);

        builder.Property(x => x.RaisedAt).IsRequired();
        builder.Property(x => x.ResolvedAt);

        // Lines stored as JSON column — always read as a unit, no cross-line queries needed
        builder.Property(x => x.Lines)
            .HasColumnName("Lines")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, SerializerOptions),
                v => JsonSerializer.Deserialize<List<FeeAppointmentApprovalLine>>(v, SerializerOptions) ?? new List<FeeAppointmentApprovalLine>(),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<FeeAppointmentApprovalLine>>(
                    (a, b) => JsonSerializer.Serialize(a, SerializerOptions) == JsonSerializer.Serialize(b, SerializerOptions),
                    v => v == null ? 0 : JsonSerializer.Serialize(v, SerializerOptions).GetHashCode(),
                    v => JsonSerializer.Deserialize<List<FeeAppointmentApprovalLine>>(JsonSerializer.Serialize(v, SerializerOptions), SerializerOptions) ?? new List<FeeAppointmentApprovalLine>()))
            .IsRequired();

        builder.HasIndex(x => x.AppraisalId)
            .HasDatabaseName("IX_FeeAppointmentApprovals_AppraisalId");

        builder.HasIndex(x => new { x.AppraisalId, x.Status })
            .HasDatabaseName("IX_FeeAppointmentApprovals_AppraisalId_Status");

        builder.HasIndex(x => x.FollowupWorkflowInstanceId)
            .HasDatabaseName("IX_FeeAppointmentApprovals_FollowupWorkflowInstanceId");
    }
}

public class FeeApprovalTierConfiguration : IEntityTypeConfiguration<FeeApprovalTier>
{
    public void Configure(EntityTypeBuilder<FeeApprovalTier> builder)
    {
        builder.ToTable("FeeApprovalTiers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MinAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(x => x.MaxAmount).HasPrecision(18, 2);

        builder.Property(x => x.ApproverCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.AssignedType)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(x => x.TierLabel)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Priority).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.AppliesTo)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("Ext");
    }
}

public class AppointmentApprovalRuleConfiguration : IEntityTypeConfiguration<AppointmentApprovalRule>
{
    public void Configure(EntityTypeBuilder<AppointmentApprovalRule> builder)
    {
        builder.ToTable("AppointmentApprovalRules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WeekendHolidayEnabled).IsRequired();
        builder.Property(x => x.LeadTimeEnabled).IsRequired();
        builder.Property(x => x.LeadTimeDays);
        builder.Property(x => x.RescheduleEnabled).IsRequired();
        builder.Property(x => x.RescheduleThreshold);

        builder.Property(x => x.AppliesTo)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("Ext");
    }
}

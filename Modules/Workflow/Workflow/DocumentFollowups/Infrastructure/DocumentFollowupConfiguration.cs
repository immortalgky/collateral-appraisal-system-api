using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.DocumentFollowups.Domain;

namespace Workflow.DocumentFollowups.Infrastructure;

public class DocumentFollowupConfiguration : IEntityTypeConfiguration<DocumentFollowup>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<DocumentFollowup> builder)
    {
        builder.ToTable("DocumentFollowups");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AppraisalId).IsRequired();
        builder.Property(x => x.RequestId);
        builder.Property(x => x.RaisingWorkflowInstanceId).IsRequired();
        builder.Property(x => x.RaisingPendingTaskId).IsRequired();

        builder.Property(x => x.RaisingActivityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.RaisingUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FollowupWorkflowInstanceId);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.CancellationReason)
            .HasMaxLength(1000);

        builder.Property(x => x.RaisedAt).IsRequired();
        builder.Property(x => x.ResolvedAt);

        // Line items stored as a single JSON column. Each followup typically has < 10 items
        // and they're always read as a unit. The gate query never inspects line items — it
        // only checks the aggregate Status — so JSON storage is fine for the hot path.
        builder.Property(x => x.LineItems)
            .HasColumnName("LineItems")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, SerializerOptions),
                v => JsonSerializer.Deserialize<List<DocumentFollowupLineItem>>(v, SerializerOptions) ?? new List<DocumentFollowupLineItem>(),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<DocumentFollowupLineItem>>(
                    (a, b) => JsonSerializer.Serialize(a, SerializerOptions) == JsonSerializer.Serialize(b, SerializerOptions),
                    v => v == null ? 0 : JsonSerializer.Serialize(v, SerializerOptions).GetHashCode(),
                    v => JsonSerializer.Deserialize<List<DocumentFollowupLineItem>>(JsonSerializer.Serialize(v, SerializerOptions), SerializerOptions) ?? new List<DocumentFollowupLineItem>()))
            .IsRequired();

        // Hot path: gate query filters by RaisingPendingTaskId + Status
        builder.HasIndex(x => new { x.RaisingPendingTaskId, x.Status })
            .HasDatabaseName("IX_DocumentFollowups_RaisingPendingTaskId_Status");

        // Enforce "at most one OPEN followup per raising task" at the database level so
        // concurrent raises cannot both pass the check-then-insert guard in the handler.
        builder.HasIndex(x => x.RaisingPendingTaskId)
            .HasDatabaseName("UX_DocumentFollowups_RaisingPendingTaskId_Open")
            .IsUnique()
            .HasFilter("[Status] = 'Open'");

        builder.HasIndex(x => x.RaisingWorkflowInstanceId)
            .HasDatabaseName("IX_DocumentFollowups_RaisingWorkflowInstanceId");

        builder.HasIndex(x => new { x.RequestId, x.Status })
            .HasDatabaseName("IX_DocumentFollowups_RequestId_Status");

        builder.HasIndex(x => x.FollowupWorkflowInstanceId)
            .HasDatabaseName("IX_DocumentFollowups_FollowupWorkflowInstanceId");
    }
}

namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for the Appraisal aggregate root.
/// </summary>
public class AppraisalAggregateConfiguration : IEntityTypeConfiguration<Domain.Appraisals.Appraisal>
{
    public void Configure(EntityTypeBuilder<Domain.Appraisals.Appraisal> builder)
    {
        builder.ToTable("Appraisals");

        // Primary Key
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Core Properties
        builder.Property(a => a.AppraisalNumber)
            .HasMaxLength(50);

        builder.Property(a => a.RequestId)
            .IsRequired();

        builder.Property(a => a.AppraisalType)
            .IsRequired()
            .HasMaxLength(50);

        // For ConstructionInspection appraisals — links to the prior appraisal whose
        // engagement supplies the CI fee (and other CI-specific carry-overs).
        builder.Property(a => a.PrevAppraisalId);
        builder.HasIndex(a => a.PrevAppraisalId)
            .HasFilter("[PrevAppraisalId] IS NOT NULL");

        builder.Property(a => a.Priority)
            .HasConversion(
                v => v.Code,
                v => Domain.Appraisals.Priority.FromDatabase(v)
            )
            .IsRequired()
            .HasMaxLength(20);

        // Request-level properties for workflow routing
        builder.Property(a => a.IsPma)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.Purpose)
            .HasMaxLength(200);

        builder.Property(a => a.Channel)
            .HasMaxLength(100);

        builder.Property(a => a.BankingSegment)
            .HasMaxLength(100);

        builder.Property(a => a.FacilityLimit)
            .HasColumnType("decimal(18,2)");

        builder.Property(a => a.HasAppraisalBook)
            .IsRequired()
            .HasDefaultValue(false);

        // Request metadata denormalized from Request aggregate
        builder.Property(a => a.RequestedBy)
            .HasMaxLength(200);

        builder.Property(a => a.RequestedAt);

        // Reappraisal batch group tag — system-assigned, NULL for non-reappraisal appraisals.
        builder.Property(a => a.GroupTag)
            .HasMaxLength(40);
        builder.HasIndex(a => a.GroupTag)
            .HasDatabaseName("IX_Appraisals_GroupTag")
            .HasFilter("[GroupTag] IS NOT NULL");

        // Status Value Object (stored as string)
        builder.OwnsOne(a => a.Status, status =>
        {
            status.Property(s => s.Code)
                .HasColumnName("Status")
                .IsRequired()
                .HasMaxLength(30);

            status.HasIndex(s => s.Code)
                .HasDatabaseName("IX_Appraisals_Status")
                .HasFilter("[IsDeleted] = 0");
        });

        // Committee approval evidence
        builder.Property(a => a.CompletedAt);
        builder.Property(a => a.ApprovedByCommittee)
            .HasMaxLength(50);

        // SLA Tracking
        builder.Property(a => a.SLAHours).HasColumnName("SLAHours");
        builder.Property(a => a.SLADueDate);
        builder.Property(a => a.SLAStatus)
            .HasMaxLength(20);
        builder.Property(a => a.ActualHoursToComplete).HasColumnName("ActualHoursToComplete");
        builder.Property(a => a.IsWithinSLA);

        // Audit Fields
        builder.Property(a => a.CreatedAt)
            .IsRequired();
        builder.Property(a => a.CreatedBy)
            .IsRequired();
        builder.Property(a => a.UpdatedAt);
        builder.Property(a => a.UpdatedBy);

        // SoftDelete Value Object
        builder.OwnsOne(a => a.SoftDelete, sd =>
        {
            sd.Property(s => s.IsDeleted)
                .HasColumnName("IsDeleted")
                .IsRequired()
                .HasDefaultValue(false);
            sd.Property(s => s.DeletedOn)
                .HasColumnName("DeletedOn");
            sd.Property(s => s.DeletedBy)
                .HasColumnName("DeletedBy");
        });

        // Navigation to child entities
        builder.HasMany(a => a.Properties)
            .WithOne()
            .HasForeignKey(p => p.AppraisalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(a => a.Groups, group =>
        {
            group.ToTable("PropertyGroups");
            group.WithOwner().HasForeignKey("AppraisalId");

            // Primary Key
            group.HasKey(g => g.Id);
            group.Property(g => g.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedNever();

            // Core Properties
            group.Property(g => g.AppraisalId)
                .IsRequired()
                .HasColumnName("AppraisalId");

            group.Property(g => g.GroupNumber)
                .IsRequired()
                .HasColumnName("GroupNumber");

            group.Property(g => g.GroupName)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("GroupName");

            group.Property(g => g.Description)
                .HasMaxLength(500)
                .HasColumnName("Description");

            // Navigation to items
            group.OwnsMany(g => g.Items, item =>
            {
                item.ToTable("PropertyGroupItems");

                // Primary Key
                item.HasKey(i => i.Id);
                item.Property(i => i.Id)
                    .HasDefaultValueSql("NEWSEQUENTIALID()")
                    .ValueGeneratedNever();

                // Core Properties
                item.Property(i => i.PropertyGroupId)
                    .IsRequired()
                    .HasColumnName("PropertyGroupId");

                item.Property(i => i.AppraisalPropertyId)
                    .IsRequired()
                    .HasColumnName("AppraisalPropertyId");

                item.Property(i => i.SequenceInGroup)
                    .IsRequired()
                    .HasColumnName("SequenceInGroup");

                // Indexes
                item.HasIndex(i => i.PropertyGroupId);
                item.HasIndex(i => i.AppraisalPropertyId);
                item.HasIndex(i => new { i.PropertyGroupId, i.AppraisalPropertyId })
                    .IsUnique();
            });

            // Indexes
            group.HasIndex(g => g.AppraisalId);
            // Non-unique: GroupNumber is system-assigned and resequenced on delete.
            // A unique index forces EF to order the renumber UPDATEs, which it cannot
            // do across a unique index in one SaveChanges (circular-dependency error).
            // Identity is PropertyGroup.Id; nothing keys off GroupNumber, so uniqueness
            // is redundant. Kept as a plain index for ordering/lookup by group number.
            group.HasIndex(g => new { g.AppraisalId, g.GroupNumber });
        });

        builder.HasMany(a => a.Assignments)
            .WithOne()
            .HasForeignKey(a => a.AppraisalId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backing fields for collections
        builder.Navigation(a => a.Properties)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(a => a.Groups)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(a => a.Assignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        // INCLUDE(RequestId) makes a prefix search (AppraisalNumber LIKE 'term%') a COVERING seek
        // in workflow.sp_GetTaskList's #f_search build (returns RequestId without a clustered scan)
        // — dropped that arm from ~2,906 logical reads to ~3 in testing.
        builder.HasIndex(a => a.AppraisalNumber)
            .IsUnique()
            .IncludeProperties(a => a.RequestId)
            .HasFilter("[AppraisalNumber] IS NOT NULL");

        builder.HasIndex(a => a.RequestId);

        // Index 2: filtered index on IsDeleted = 0 to support WHERE a.IsDeleted = 0 in vw_AppraisalList
        builder.HasIndex(a => a.Id)
            .HasDatabaseName("IX_Appraisals_IsDeleted_NotDeleted")
            .HasFilter("[IsDeleted] = 0");

        // Ignore domain events (not persisted)
        builder.Ignore(a => a.DomainEvents);
    }
}
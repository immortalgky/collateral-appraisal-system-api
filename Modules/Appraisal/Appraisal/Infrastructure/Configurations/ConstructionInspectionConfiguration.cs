namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for ConstructionInspection (owned 1:1 by AppraisalProperty).
/// Stored in separate table with nested OwnsMany for ConstructionWorkDetails.
/// </summary>
public class
    ConstructionInspectionConfiguration : IOwnedEntityConfiguration<AppraisalProperty, ConstructionInspection>
{
    public void Configure(OwnedNavigationBuilder<AppraisalProperty, ConstructionInspection> builder)
    {
        builder.ToTable("ConstructionInspections", "appraisal");
        builder.WithOwner().HasForeignKey(e => e.AppraisalPropertyId);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with AppraisalProperties
        builder.HasIndex(e => e.AppraisalPropertyId).IsUnique();

        // Mode
        builder.Property(e => e.IsFullDetail).IsRequired();
        builder.Property(e => e.TotalValue).HasPrecision(18, 2);

        // Summary Mode fields
        builder.Property(e => e.SummaryDetail).HasMaxLength(1000);
        builder.Property(e => e.SummaryPreviousProgressPct).HasPrecision(7, 4);
        builder.Property(e => e.SummaryPreviousValue).HasPrecision(18, 2);
        builder.Property(e => e.SummaryCurrentProgressPct).HasPrecision(7, 4);
        builder.Property(e => e.SummaryCurrentValue).HasPrecision(18, 2);
        builder.Property(e => e.Remark).HasMaxLength(4000);

        // Document reference
        builder.Property(e => e.FileName).HasMaxLength(500);
        builder.Property(e => e.FilePath).HasMaxLength(1000);
        builder.Property(e => e.FileExtension).HasMaxLength(20);
        builder.Property(e => e.MimeType).HasMaxLength(100);

        // Work Details - Owned collection (full detail mode)
        builder.OwnsMany(e => e.WorkDetails, detail =>
        {
            detail.ToTable("ConstructionWorkDetails", "appraisal");
            detail.WithOwner().HasForeignKey(d => d.ConstructionInspectionId);
            detail.HasKey(d => d.Id);
            detail.Property(d => d.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

            detail.Property(d => d.ConstructionWorkGroupId).IsRequired();
            detail.Property(d => d.WorkItemName).IsRequired().HasMaxLength(200);
            detail.Property(d => d.DisplayOrder).IsRequired();

            // User-entered values
            detail.Property(d => d.ConstructionValue).HasPrecision(18, 2);
            detail.Property(d => d.PreviousProgressPct).HasPrecision(7, 4);
            detail.Property(d => d.CurrentProgressPct).HasPrecision(7, 4);

            // Computed values
            detail.Property(d => d.ProportionPct).HasPrecision(7, 4);
            detail.Property(d => d.CurrentProportionPct).HasPrecision(7, 4);
            detail.Property(d => d.PreviousPropertyValue).HasPrecision(18, 2);
            detail.Property(d => d.CurrentPropertyValue).HasPrecision(18, 2);

            detail.HasIndex(d => d.ConstructionInspectionId);
            detail.HasIndex(d => d.ConstructionWorkGroupId);
        });
    }
}

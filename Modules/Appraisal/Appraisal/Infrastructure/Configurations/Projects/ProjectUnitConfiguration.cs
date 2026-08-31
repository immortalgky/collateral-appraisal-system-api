namespace Appraisal.Infrastructure.Configurations.Projects;

public class ProjectUnitConfiguration : IEntityTypeConfiguration<ProjectUnit>
{
    public void Configure(EntityTypeBuilder<ProjectUnit> builder)
    {
        builder.ToTable("ProjectUnits");

        // Primary Key — no server default
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Foreign Key to Project
        builder.Property(e => e.ProjectId).IsRequired();
        builder.HasIndex(e => e.ProjectId);

        // Upload Batch FK
        builder.Property(e => e.UploadBatchId).IsRequired();
        builder.HasIndex(e => e.UploadBatchId);

        // Composite index for fast per-project sequence lookups
        builder.HasIndex(e => new { e.ProjectId, e.SequenceNumber });

        // Core Properties
        builder.Property(e => e.SequenceNumber).IsRequired();

        // Unit number — minted on approval, never before. Format {YY}U{running:D5} (8 chars),
        // e.g. "69U00042". Null while the appraisal is still being worked on, which is what makes
        // the "may this unit set still be replaced?" guard in Project work.
        builder.Property(e => e.UnitNumber).HasMaxLength(10);
        builder.HasIndex(e => e.UnitNumber)
            .IsUnique()
            .HasFilter("[UnitNumber] IS NOT NULL")
            .HasDatabaseName("UX_ProjectUnits_UnitNumber");
        builder.Property(e => e.ModelType).HasMaxLength(200);
        builder.Property(e => e.UsableArea).HasPrecision(10, 2);
        builder.Property(e => e.SellingPrice).HasPrecision(18, 2);

        // Condo-side
        builder.Property(e => e.TowerName).HasMaxLength(200);
        builder.Property(e => e.CondoRegistrationNumber).HasMaxLength(100);
        builder.Property(e => e.RoomNumber).HasMaxLength(50);

        // LB-side
        builder.Property(e => e.PlotNumber).HasMaxLength(100);
        builder.Property(e => e.HouseNumber).HasMaxLength(100);
        builder.Property(e => e.LandArea).HasPrecision(10, 2);

        // Sale tracking
        builder.Property(e => e.IsSold)
            .IsRequired()
            .HasDefaultValue(false);

        // Stored as the enum NAME ("Cash" / "Loan") so SQL queries and JSON wire format
        // align without a CASE expression (the unit-maintenance handler used to translate
        // int → string in SQL; that translation is now gone).
        builder.Property(e => e.PurchaseBy)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired(false);

        builder.Property(e => e.LoanBankName)
            .HasMaxLength(200)
            .IsRequired(false);

        // Optional FK to ProjectTower (Condo path) — NoAction to avoid multiple cascade paths
        builder.HasOne<ProjectTower>()
            .WithMany()
            .HasForeignKey(u => u.ProjectTowerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        // Optional FK to ProjectModel (both paths) — NoAction
        builder.HasOne<ProjectModel>()
            .WithMany()
            .HasForeignKey(u => u.ProjectModelId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

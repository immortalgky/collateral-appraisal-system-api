namespace Appraisal.Infrastructure.Configurations;

public class VillageUnitConfiguration : IEntityTypeConfiguration<VillageUnit>
{
    public void Configure(EntityTypeBuilder<VillageUnit> builder)
    {
        builder.ToTable("VillageUnits");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Keys
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.HasIndex(e => e.AppraisalId);

        builder.Property(e => e.UploadBatchId).IsRequired();
        builder.HasIndex(e => e.UploadBatchId);

        // Composite Index
        builder.HasIndex(e => new { e.AppraisalId, e.SequenceNumber });

        // Core Properties
        builder.Property(e => e.SequenceNumber).IsRequired();
        builder.Property(e => e.PlotNumber).HasMaxLength(50);
        builder.Property(e => e.HouseNumber).HasMaxLength(50);
        builder.Property(e => e.ModelName).HasMaxLength(200);

        // Optional FK to VillageModel
        builder.HasOne<VillageModel>().WithMany().HasForeignKey(u => u.VillageModelId).OnDelete(DeleteBehavior.SetNull);

        // Precision
        builder.Property(e => e.LandArea).HasPrecision(10, 2);
        builder.Property(e => e.UsableArea).HasPrecision(10, 2);
        builder.Property(e => e.SellingPrice).HasPrecision(18, 2);
    }
}

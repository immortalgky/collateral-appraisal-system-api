namespace Appraisal.Infrastructure.Configurations;

public class CondoUnitConfiguration : IEntityTypeConfiguration<CondoUnit>
{
    public void Configure(EntityTypeBuilder<CondoUnit> builder)
    {
        builder.ToTable("CondoUnits");

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
        builder.Property(e => e.TowerName).HasMaxLength(200);
        builder.Property(e => e.CondoRegistrationNumber).HasMaxLength(100);
        builder.Property(e => e.RoomNumber).HasMaxLength(50);
        builder.Property(e => e.ModelType).HasMaxLength(200);

        // Optional FK to CondoTower and CondoModel
        builder.HasOne<CondoTower>().WithMany().HasForeignKey(u => u.CondoTowerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<CondoModel>().WithMany().HasForeignKey(u => u.CondoModelId).OnDelete(DeleteBehavior.SetNull);

        // Precision
        builder.Property(e => e.UsableArea).HasPrecision(10, 2);
        builder.Property(e => e.SellingPrice).HasPrecision(18, 2);
    }
}

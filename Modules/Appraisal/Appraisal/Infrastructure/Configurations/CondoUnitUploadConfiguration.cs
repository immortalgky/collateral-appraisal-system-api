namespace Appraisal.Infrastructure.Configurations;

public class CondoUnitUploadConfiguration : IEntityTypeConfiguration<CondoUnitUpload>
{
    public void Configure(EntityTypeBuilder<CondoUnitUpload> builder)
    {
        builder.ToTable("CondoUnitUploads");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Key
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.HasIndex(e => e.AppraisalId);

        // Core Properties
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.UploadedAt).IsRequired();
        builder.Property(e => e.IsUsed).IsRequired().HasDefaultValue(false);
    }
}

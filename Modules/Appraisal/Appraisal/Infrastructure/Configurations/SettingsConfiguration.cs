namespace Appraisal.Infrastructure.Configurations;

public class AppraisalSettingsConfiguration : IEntityTypeConfiguration<AppraisalSettings>
{
    public void Configure(EntityTypeBuilder<AppraisalSettings> builder)
    {
        builder.ToTable("AppraisalSettings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.SettingKey).IsRequired().HasMaxLength(100);
        builder.Property(s => s.SettingValue).IsRequired().HasMaxLength(500);
        builder.Property(s => s.Description).HasMaxLength(500);

        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Property(s => s.UpdatedBy).IsRequired();

        builder.HasIndex(s => s.SettingKey).IsUnique();
    }
}

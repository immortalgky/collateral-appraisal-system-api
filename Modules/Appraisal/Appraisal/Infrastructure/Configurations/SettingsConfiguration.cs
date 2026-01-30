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

        builder.Property(s => s.UpdatedOn).IsRequired();
        builder.Property(s => s.UpdatedBy).IsRequired();

        builder.HasIndex(s => s.SettingKey).IsUnique();
    }
}

public class AutoAssignmentRuleConfiguration : IEntityTypeConfiguration<AutoAssignmentRule>
{
    public void Configure(EntityTypeBuilder<AutoAssignmentRule> builder)
    {
        builder.ToTable("AutoAssignmentRules");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.RuleName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Priority).IsRequired();

        // JSON conditions
        builder.Property(r => r.PropertyTypes).HasMaxLength(1000);
        builder.Property(r => r.Provinces).HasMaxLength(1000);
        builder.Property(r => r.MinEstimatedValue).HasPrecision(18, 2);
        builder.Property(r => r.MaxEstimatedValue).HasPrecision(18, 2);
        builder.Property(r => r.LoanTypes).HasMaxLength(1000);
        builder.Property(r => r.Priorities).HasMaxLength(500);

        builder.Property(r => r.AssignmentMode).IsRequired().HasMaxLength(50);

        builder.Property(r => r.CreatedOn).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();

        builder.HasIndex(r => r.Priority);
        builder.HasIndex(r => r.IsActive);
    }
}
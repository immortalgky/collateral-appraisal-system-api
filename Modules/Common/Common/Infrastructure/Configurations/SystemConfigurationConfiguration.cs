using Common.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
    {
        builder.ToTable("SystemConfigurations", "common");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(SystemConfiguration.MaxKeyLength);

        builder.Property(s => s.Value)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(s => s.ValueType)
            .IsRequired()
            .HasMaxLength(SystemConfiguration.MaxValueTypeLength)
            .HasDefaultValue("string");

        builder.Property(s => s.Description)
            .HasColumnType("nvarchar(max)");

        builder.Property(s => s.Category)
            .HasMaxLength(SystemConfiguration.MaxCategoryLength);

        builder.Property(s => s.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(s => s.Key)
            .IsUnique()
            .HasDatabaseName("UX_SystemConfigurations_Key");
    }
}

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Parameter.Data.Configurations;

public class ParameterConfiguration : IEntityTypeConfiguration<Parameters.Models.Parameter>
{
    public void Configure(EntityTypeBuilder<Parameters.Models.Parameter> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ParId").UseIdentityColumn();

        builder.Property(p => p.Group).HasMaxLength(50);
        builder.Property(p => p.Country).HasMaxLength(10);
        builder.Property(p => p.Language).HasMaxLength(10);
        builder.Property(p => p.Code).HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(500);

        builder.HasIndex(p => new { p.Group, p.Country, p.Language, p.IsActive })
            .HasDatabaseName("IX_Parameters_Group_Country_Language_IsActive");

        builder.HasIndex(p => new { p.Group, p.Country, p.Language, p.Code })
            .IsUnique()
            .HasDatabaseName("UQ_Parameters_Group_Country_Language_Code");
    }
}

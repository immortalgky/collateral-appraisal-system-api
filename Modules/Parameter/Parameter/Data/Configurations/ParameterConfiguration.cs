using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Parameter.Data.Configurations;

public class ParameterConfiguration : IEntityTypeConfiguration<Parameters.Models.Parameter>
{
    public void Configure(EntityTypeBuilder<Parameters.Models.Parameter> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ParId").UseIdentityColumn();
    }
}
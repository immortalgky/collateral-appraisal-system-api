using Workflow.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Workflow.Data.Configurations;

public class CompanyRoundRobinConfigurationConfiguration : IEntityTypeConfiguration<CompanyRoundRobinConfiguration>
{
    public void Configure(EntityTypeBuilder<CompanyRoundRobinConfiguration> builder)
    {
        builder.ToTable("CompanyRoundRobinConfigurations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LoanType)
            .HasMaxLength(50);

        builder.Property(x => x.Entries)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.UpdatedBy)
            .IsRequired()
            .HasMaxLength(100);

        // One active pool per loan-type scope. Filtered to active rows so soft-disabled rows can coexist.
        builder.HasIndex(x => x.LoanType)
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_CompanyRoundRobinConfigurations_LoanType_Active");
    }
}

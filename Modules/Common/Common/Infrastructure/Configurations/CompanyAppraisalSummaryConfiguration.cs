using Common.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class CompanyAppraisalSummaryConfiguration : IEntityTypeConfiguration<CompanyAppraisalSummary>
{
    public void Configure(EntityTypeBuilder<CompanyAppraisalSummary> builder)
    {
        builder.ToTable("CompanyAppraisalSummaries");

        builder.HasKey(x => x.CompanyId);

        builder.Property(x => x.CompanyName)
            .HasMaxLength(255);
    }
}

using Common.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class CompanyAppraisalSummaryConfiguration : IEntityTypeConfiguration<CompanyAppraisalSummary>
{
    public void Configure(EntityTypeBuilder<CompanyAppraisalSummary> builder)
    {
        builder.ToTable("CompanyAppraisalSummaries");

        builder.HasKey(x => new { x.CompanyId, x.Date });

        builder.Property(x => x.Date)
            .HasColumnType("date");

        builder.Property(x => x.CompanyName)
            .HasMaxLength(255);
    }
}

using Common.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class DailyAppraisalCountConfiguration : IEntityTypeConfiguration<DailyAppraisalCount>
{
    public void Configure(EntityTypeBuilder<DailyAppraisalCount> builder)
    {
        builder.ToTable("DailyAppraisalCounts");

        builder.HasKey(x => x.Date);

        builder.Property(x => x.Date)
            .HasColumnType("date");
    }
}

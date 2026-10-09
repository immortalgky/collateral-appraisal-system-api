using Appraisal.Domain.Appraisals;

namespace Appraisal.Infrastructure.Configurations;

public class FireInsuranceRateConfiguration : IEntityTypeConfiguration<FireInsuranceRate>
{
    public void Configure(EntityTypeBuilder<FireInsuranceRate> builder)
    {
        builder.ToTable("FireInsuranceRates");

        builder.HasKey(r => r.Code);
        builder.Property(r => r.Code).IsRequired().HasMaxLength(10).ValueGeneratedNever();
        builder.Property(r => r.Condition).IsRequired().HasMaxLength(200);
        builder.HasIndex(r => r.Condition).IsUnique();
        builder.Property(r => r.PropertyKind).IsRequired().HasMaxLength(50);
        builder.Property(r => r.RatePerSqm).IsRequired().HasPrecision(18, 2);
        builder.Property(r => r.DisplaySeq).IsRequired();
    }
}

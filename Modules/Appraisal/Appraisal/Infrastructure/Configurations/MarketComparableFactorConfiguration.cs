namespace Appraisal.Infrastructure.Configurations;

public class MarketComparableFactorConfiguration : IEntityTypeConfiguration<MarketComparableFactor>
{
    public void Configure(EntityTypeBuilder<MarketComparableFactor> builder)
    {
        builder.ToTable("MarketComparableFactors");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(f => f.FactorCode).IsRequired().HasMaxLength(50);
        builder.Property(f => f.FactorName).IsRequired().HasMaxLength(200);
        builder.Property(f => f.FieldName).IsRequired().HasMaxLength(100);
        builder.Property(f => f.DataType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.FieldLength);
        builder.Property(f => f.FieldDecimal);
        builder.Property(f => f.ParameterGroup).HasMaxLength(100);
        builder.Property(f => f.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(f => f.CreatedOn).IsRequired();
        builder.Property(f => f.CreatedBy).IsRequired();

        builder.HasIndex(f => f.FactorCode).IsUnique();
        builder.HasIndex(f => f.IsActive);
    }
}

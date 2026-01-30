namespace Appraisal.Infrastructure.Configurations;

public class MarketComparableDataConfiguration : IEntityTypeConfiguration<MarketComparableData>
{
    public void Configure(EntityTypeBuilder<MarketComparableData> builder)
    {
        builder.ToTable("MarketComparableData");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(d => d.MarketComparableId).IsRequired();
        builder.Property(d => d.FactorId).IsRequired();
        builder.Property(d => d.Value); // NVARCHAR(MAX)
        builder.Property(d => d.OtherRemarks).HasMaxLength(500);

        builder.Property(d => d.CreatedOn).IsRequired();
        builder.Property(d => d.CreatedBy).IsRequired();

        builder.HasIndex(d => new { d.MarketComparableId, d.FactorId }).IsUnique();
        builder.HasIndex(d => d.MarketComparableId);
        builder.HasIndex(d => d.FactorId);

        // Navigation to Factor for eager loading
        builder.HasOne(d => d.Factor)
            .WithMany()
            .HasForeignKey(d => d.FactorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

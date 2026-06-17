using Appraisal.Domain.Appraisals;

namespace Appraisal.Infrastructure.Configurations;

public class
    AssetSummaryConfiguration : IEntityTypeConfiguration<AssetSummary>
{
    public void Configure(EntityTypeBuilder<AssetSummary> builder)
    {
        builder.ToTable("AssetSummary", "appraisal");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AppraisalId).IsRequired();
        builder.HasIndex(x => x.AppraisalId);

        builder.Property(x => x.AssetDetail).HasMaxLength(4000);
        builder.Property(x => x.Area).HasPrecision(19, 4);
        builder.Property(x => x.PricePerUnit).HasPrecision(19, 4);
        builder.Property(x => x.EstimatedPrice).HasPrecision(19, 4);
        builder.Property(x => x.CurrentPrice).HasPrecision(19, 4);
        builder.Property(x => x.GroupSet);
        builder.Property(x => x.IsPricesCurrent);
    }
}

public class
    AssetSummaryGroupConfiguration : IEntityTypeConfiguration<AssetSummaryGroup>
{
    public void Configure(EntityTypeBuilder<AssetSummaryGroup> builder)
    {
        builder.ToTable("AssetSummaryGroup", "appraisal");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AppraisalId).IsRequired();
        builder.HasIndex(x => x.AppraisalId);
        builder.Property(x => x.GroupSet).IsRequired();

        builder.Property(x => x.AssetGroupDetail).HasMaxLength(200);
        builder.Property(x => x.SumEstimatedPrice).HasPrecision(19, 4);
        builder.Property(x => x.RoundEstimatedPrice).HasPrecision(19, 4);
        builder.Property(x => x.SumCurrentPrice).HasPrecision(19, 4);
        builder.Property(x => x.RoundCurrentPrice).HasPrecision(19, 4);
    }
}
namespace Appraisal.Infrastructure.Configurations;

public class RentalInfoConfiguration : IOwnedEntityConfiguration<AppraisalProperty, RentalInfo>
{
    public void Configure(OwnedNavigationBuilder<AppraisalProperty, RentalInfo> builder)
    {
        builder.ToTable("RentalInfos", "appraisal");
        builder.WithOwner().HasForeignKey(e => e.AppraisalPropertyId);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with AppraisalProperties
        builder.HasIndex(e => e.AppraisalPropertyId).IsUnique();

        // Schedule header
        builder.Property(e => e.ContractRentalFeePerYear).HasPrecision(18, 2);
        builder.Property(e => e.UpFrontTotalAmount).HasPrecision(18, 2);

        // Growth rate
        builder.Property(e => e.GrowthRateType).HasMaxLength(20);
        builder.Property(e => e.GrowthRatePercent).HasPrecision(10, 4);

        builder.Property(e => e.AppraisalPropertyId).IsRequired();

        // Up-front entries (OwnsMany)
        builder.OwnsMany(e => e.UpFrontEntries, uf =>
        {
            uf.ToTable("RentalUpFrontEntries", "appraisal");
            uf.WithOwner().HasForeignKey(e => e.RentalInfoId);
            uf.HasKey(e => e.Id);
            uf.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            uf.Property(e => e.UpFrontAmount).HasPrecision(18, 2);
            uf.HasIndex(e => e.RentalInfoId);
        });

        // Growth period entries (OwnsMany)
        builder.OwnsMany(e => e.GrowthPeriodEntries, gp =>
        {
            gp.ToTable("RentalGrowthPeriodEntries", "appraisal");
            gp.WithOwner().HasForeignKey(e => e.RentalInfoId);
            gp.HasKey(e => e.Id);
            gp.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            gp.Property(e => e.GrowthRate).HasPrecision(10, 4);
            gp.Property(e => e.GrowthAmount).HasPrecision(18, 2);
            gp.Property(e => e.TotalAmount).HasPrecision(18, 2);
            gp.HasIndex(e => e.RentalInfoId);
        });

        // Schedule entries (OwnsMany) — full computed+overridden rows
        builder.OwnsMany(e => e.ScheduleEntries, se =>
        {
            se.ToTable("RentalScheduleEntries", "appraisal");
            se.WithOwner().HasForeignKey(e => e.RentalInfoId);
            se.HasKey(e => e.Id);
            se.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            se.Property(e => e.UpFront).HasPrecision(18, 2);
            se.Property(e => e.ContractRentalFee).HasPrecision(18, 2);
            se.Property(e => e.TotalAmount).HasPrecision(18, 2);
            se.Property(e => e.ContractRentalFeeGrowthRatePercent).HasPrecision(10, 4);
            se.HasIndex(e => e.RentalInfoId);
        });

        // Schedule overrides (OwnsMany) — user-edited cells only
        builder.OwnsMany(e => e.ScheduleOverrides, so =>
        {
            so.ToTable("RentalScheduleOverrides", "appraisal");
            so.WithOwner().HasForeignKey(e => e.RentalInfoId);
            so.HasKey(e => e.Id);
            so.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            so.Property(e => e.UpFront).HasPrecision(18, 2);
            so.Property(e => e.ContractRentalFee).HasPrecision(18, 2);
            so.HasIndex(e => e.RentalInfoId);
        });
    }
}

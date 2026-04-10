namespace Appraisal.Infrastructure.Configurations;

public class MachineryAppraisalSummaryConfiguration : IEntityTypeConfiguration<MachineryAppraisalSummary>
{
    public void Configure(EntityTypeBuilder<MachineryAppraisalSummary> builder)
    {
        builder.ToTable("MachineryAppraisalSummaries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with Appraisal via unique index
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.HasIndex(e => e.AppraisalId).IsUnique();

        // Section 3.1 — General Machinery
        builder.Property(e => e.InIndustrial).HasMaxLength(500);
        builder.Property(e => e.Maintenance).HasMaxLength(500);
        builder.Property(e => e.Exterior).HasMaxLength(500);
        builder.Property(e => e.Performance).HasMaxLength(500);
        builder.Property(e => e.MarketDemand).HasMaxLength(4000);

        // Section 3.3 — Rights & Legal
        builder.Property(e => e.Proprietor).HasMaxLength(500);
        builder.Property(e => e.Owner).HasMaxLength(500);
        builder.Property(e => e.MachineAddress).HasMaxLength(1000);
        builder.Property(e => e.Latitude).HasPrecision(11, 8);
        builder.Property(e => e.Longitude).HasPrecision(11, 8);
        builder.Property(e => e.Obligation).HasMaxLength(2000);
        builder.Property(e => e.Other).HasMaxLength(4000);
    }
}

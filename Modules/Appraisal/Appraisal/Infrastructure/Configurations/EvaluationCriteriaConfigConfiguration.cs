using Appraisal.Domain.Evaluations;

namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for EvaluationCriteriaConfig.
/// Table: appraisal.EvaluationCriteriaConfigs
/// Unique constraint: (BankingSegment, CriteriaSlot) — one row per segment per slot.
/// </summary>
public class EvaluationCriteriaConfigConfiguration : IEntityTypeConfiguration<EvaluationCriteriaConfig>
{
    public void Configure(EntityTypeBuilder<EvaluationCriteriaConfig> builder)
    {
        builder.ToTable("EvaluationCriteriaConfigs");

        // ── Primary key ───────────────────────────────────────────────────────
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // ── Identity (immutable) ──────────────────────────────────────────────
        builder.Property(e => e.BankingSegment)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.CriteriaSlot)
            .IsRequired();

        builder.Property(e => e.CriteriaKey)
            .IsRequired()
            .HasMaxLength(100);

        // ── Labels ────────────────────────────────────────────────────────────
        builder.Property(e => e.LabelEn)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.LabelTh)
            .IsRequired()
            .HasMaxLength(500);

        // ── Score parameters ──────────────────────────────────────────────────
        builder.Property(e => e.Weight)
            .IsRequired()
            .HasPrecision(5, 4);

        builder.Property(e => e.MaxScore)
            .IsRequired();

        // ── JSON blobs ────────────────────────────────────────────────────────
        builder.Property(e => e.GuidanceJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.ThresholdsJson)
            .HasColumnType("nvarchar(max)");

        // ── Display ordering ──────────────────────────────────────────────────
        builder.Property(e => e.DisplayOrder)
            .IsRequired();

        // ── Unique index: one config row per (BankingSegment, CriteriaSlot) ──
        builder.HasIndex(e => new { e.BankingSegment, e.CriteriaSlot })
            .IsUnique()
            .HasDatabaseName("UX_EvaluationCriteriaConfigs_Segment_Slot");
    }
}

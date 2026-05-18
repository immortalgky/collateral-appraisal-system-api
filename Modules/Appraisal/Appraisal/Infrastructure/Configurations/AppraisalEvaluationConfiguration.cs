using Appraisal.Domain.Evaluations;

namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for the AppraisalEvaluation entity.
/// Table: appraisal.AppraisalEvaluations
/// </summary>
public class AppraisalEvaluationConfiguration : IEntityTypeConfiguration<AppraisalEvaluation>
{
    public void Configure(EntityTypeBuilder<AppraisalEvaluation> builder)
    {
        builder.ToTable("AppraisalEvaluations");

        // ── Primary key ─────────────────────────────────────────────────────
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // ── Core identifiers ────────────────────────────────────────────────
        builder.Property(e => e.AppraisalId)
            .IsRequired();

        builder.Property(e => e.AppraisalNumber)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("AppraisalNumber");

        // Enforce 1:1 — one evaluation per appraisal
        builder.HasIndex(e => e.AppraisalId)
            .IsUnique()
            .HasDatabaseName("UX_AppraisalEvaluations_AppraisalId");

        // ── Lifecycle ───────────────────────────────────────────────────────
        builder.Property(e => e.EvaluationStatus)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("EvaluationStatus");

        builder.Property(e => e.EvaluatedBy)
            .HasMaxLength(100)
            .HasColumnName("EvaluatedBy");

        builder.Property(e => e.EvaluatedAt)
            .HasColumnName("EvaluatedAt");

        // ── Criterion 1 ─────────────────────────────────────────────────────
        builder.Property(e => e.Criteria1Rating)
            .HasColumnName("Criteria1Rating");

        // ── Criterion 2 ─────────────────────────────────────────────────────
        builder.Property(e => e.Criteria2Rating)
            .HasColumnName("Criteria2Rating");

        builder.Property(e => e.Criteria2IsAutoDetected)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("Criteria2IsAutoDetected");

        builder.Property(e => e.Criteria2DetectedDays)
            .HasPrecision(5, 2)
            .HasColumnName("Criteria2DetectedDays");

        // ── Criterion 3 ─────────────────────────────────────────────────────
        builder.Property(e => e.Criteria3Rating)
            .HasColumnName("Criteria3Rating");

        // ── Criterion 4 ─────────────────────────────────────────────────────
        builder.Property(e => e.Criteria4Rating)
            .HasColumnName("Criteria4Rating");

        // ── Criterion 5 ─────────────────────────────────────────────────────
        builder.Property(e => e.Criteria5Rating)
            .HasColumnName("Criteria5Rating");

        // ── Free text ───────────────────────────────────────────────────────
        builder.Property(e => e.AdditionalComments)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("AdditionalComments");

        builder.Property(e => e.Note)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("Note");
    }
}

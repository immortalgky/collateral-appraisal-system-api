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
            .IsRequired()
            .HasColumnName("Criteria1Rating");

        builder.Property(e => e.Criteria1Description)
            .HasMaxLength(500)
            .HasColumnName("Criteria1Description");

        // ── Criterion 2 ─────────────────────────────────────────────────────
        builder.Property(e => e.Criteria2Rating)
            .IsRequired()
            .HasColumnName("Criteria2Rating");

        builder.Property(e => e.Criteria2IsAutoDetected)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("Criteria2IsAutoDetected");

        builder.Property(e => e.Criteria2DetectedDays)
            .HasPrecision(5, 2)
            .HasColumnName("Criteria2DetectedDays");

        builder.Property(e => e.Criteria2Description)
            .HasMaxLength(500)
            .HasColumnName("Criteria2Description");

        // ── Criterion 3 ─────────────────────────────────────────────────────
        builder.Property(e => e.Criteria3Rating)
            .IsRequired()
            .HasColumnName("Criteria3Rating");

        builder.Property(e => e.Criteria3Description)
            .HasMaxLength(500)
            .HasColumnName("Criteria3Description");

        // ── Criterion 4 ─────────────────────────────────────────────────────
        builder.Property(e => e.Criteria4Rating)
            .IsRequired()
            .HasColumnName("Criteria4Rating");

        builder.Property(e => e.Criteria4Description)
            .HasMaxLength(500)
            .HasColumnName("Criteria4Description");

        // ── Criterion 5 ─────────────────────────────────────────────────────
        builder.Property(e => e.Criteria5Rating)
            .IsRequired()
            .HasColumnName("Criteria5Rating");

        builder.Property(e => e.Criteria5Description)
            .HasMaxLength(500)
            .HasColumnName("Criteria5Description");

        // ── Free text ───────────────────────────────────────────────────────
        builder.Property(e => e.AdditionalComments)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("AdditionalComments");

        builder.Property(e => e.Note)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("Note");
    }
}

namespace Appraisal.Domain.Evaluations;

/// <summary>
/// Represents a service quality evaluation of an external appraisal company for a given appraisal.
/// One evaluation per appraisal (1:1 relationship enforced via unique index on AppraisalId).
/// Five weighted criteria produce a composite quality score.
/// </summary>
public class AppraisalEvaluation : Entity<Guid>
{
    // ── Core identifiers ────────────────────────────────────────────────────
    public Guid AppraisalId { get; private set; }

    /// <summary>Denormalized from Appraisal for reporting convenience.</summary>
    public string AppraisalNumber { get; private set; } = null!;

    // ── Lifecycle ───────────────────────────────────────────────────────────
    /// <summary>"Draft" | "Completed"</summary>
    public string EvaluationStatus { get; private set; } = null!;

    public string? EvaluatedBy { get; private set; }
    public DateTime? EvaluatedAt { get; private set; }

    // ── Criterion 1: Report book quality (weight 0.40) ──────────────────────
    public int Criteria1Rating { get; private set; }
    public string? Criteria1Description { get; private set; }

    // ── Criterion 2: Delivery time (weight 0.30, auto-detected) ────────────
    public int Criteria2Rating { get; private set; }
    public bool Criteria2IsAutoDetected { get; private set; }
    public decimal? Criteria2DetectedDays { get; private set; }
    public string? Criteria2Description { get; private set; }

    // ── Criterion 3: Company personnel preparation (weight 0.10) ───────────
    public int Criteria3Rating { get; private set; }
    public string? Criteria3Description { get; private set; }

    // ── Criterion 4: Response time to problem (weight 0.10) ────────────────
    public int Criteria4Rating { get; private set; }
    public string? Criteria4Description { get; private set; }

    // ── Criterion 5: Coordination & responsibility (weight 0.10) ───────────
    public int Criteria5Rating { get; private set; }
    public string? Criteria5Description { get; private set; }

    // ── Free text ───────────────────────────────────────────────────────────
    public string? AdditionalComments { get; private set; }
    public string? Note { get; private set; }

    // ── Private constructor for EF Core ────────────────────────────────────
    private AppraisalEvaluation()
    {
    }

    // ── Factory ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new Draft evaluation for the given appraisal.
    /// All rating fields default to 1 (lowest valid value) so the draft is
    /// immediately valid and the user can edit each criterion.
    /// </summary>
    public static AppraisalEvaluation Create(
        Guid appraisalId,
        string appraisalNumber,
        string evaluationStatus,
        string? evaluatedBy,
        int criteria1Rating,
        string? criteria1Description,
        int criteria2Rating,
        bool criteria2IsAutoDetected,
        decimal? criteria2DetectedDays,
        string? criteria2Description,
        int criteria3Rating,
        string? criteria3Description,
        int criteria4Rating,
        string? criteria4Description,
        int criteria5Rating,
        string? criteria5Description,
        string? additionalComments,
        string? note)
    {
        ValidateStatus(evaluationStatus);
        ValidateRatings(
            criteria1Rating, criteria2Rating,
            criteria3Rating, criteria4Rating, criteria5Rating);

        var evaluation = new AppraisalEvaluation
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            AppraisalNumber = appraisalNumber,
            EvaluationStatus = evaluationStatus,
            Criteria1Rating = criteria1Rating,
            Criteria1Description = criteria1Description,
            Criteria2Rating = criteria2Rating,
            Criteria2IsAutoDetected = criteria2IsAutoDetected,
            Criteria2DetectedDays = criteria2IsAutoDetected ? criteria2DetectedDays : null,
            Criteria2Description = criteria2Description,
            Criteria3Rating = criteria3Rating,
            Criteria3Description = criteria3Description,
            Criteria4Rating = criteria4Rating,
            Criteria4Description = criteria4Description,
            Criteria5Rating = criteria5Rating,
            Criteria5Description = criteria5Description,
            AdditionalComments = additionalComments,
            Note = note
        };

        if (evaluationStatus == "Completed")
        {
            evaluation.EvaluatedAt = DateTime.UtcNow;
            evaluation.EvaluatedBy = evaluatedBy;
        }

        return evaluation;
    }

    // ── Mutation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates all criteria and optional completion details.
    /// When status is "Completed", stamps EvaluatedAt / EvaluatedBy.
    /// </summary>
    public void Update(
        int criteria1Rating,
        string? criteria1Description,
        int criteria2Rating,
        bool criteria2IsAutoDetected,
        decimal? criteria2DetectedDays,
        string? criteria2Description,
        int criteria3Rating,
        string? criteria3Description,
        int criteria4Rating,
        string? criteria4Description,
        int criteria5Rating,
        string? criteria5Description,
        string? additionalComments,
        string? note,
        string evaluationStatus,
        string? evaluatedBy)
    {
        if (EvaluationStatus == "Completed")
            throw new InvalidOperationException("A completed evaluation cannot be modified.");

        ValidateRatings(
            criteria1Rating, criteria2Rating,
            criteria3Rating, criteria4Rating, criteria5Rating);

        Criteria1Rating = criteria1Rating;
        Criteria1Description = criteria1Description;
        Criteria2Rating = criteria2Rating;
        Criteria2IsAutoDetected = criteria2IsAutoDetected;
        Criteria2DetectedDays = criteria2IsAutoDetected ? criteria2DetectedDays : null;
        Criteria2Description = criteria2Description;
        Criteria3Rating = criteria3Rating;
        Criteria3Description = criteria3Description;
        Criteria4Rating = criteria4Rating;
        Criteria4Description = criteria4Description;
        Criteria5Rating = criteria5Rating;
        Criteria5Description = criteria5Description;
        AdditionalComments = additionalComments;
        Note = note;
        ValidateStatus(evaluationStatus);
        EvaluationStatus = evaluationStatus;

        if (evaluationStatus == "Completed")
        {
            EvaluatedAt = DateTime.UtcNow;
            EvaluatedBy = evaluatedBy;
        }
    }

    // ── Invariant guards ────────────────────────────────────────────────────

    private static void ValidateStatus(string status)
    {
        if (status is not ("Draft" or "Completed"))
            throw new ArgumentException(
                $"EvaluationStatus must be 'Draft' or 'Completed' (got '{status}').", nameof(status));
    }

    private static void ValidateRatings(
        int c1, int c2, int c3, int c4, int c5)
    {
        static void Check(int rating, int index)
        {
            if (rating is < 1 or > 4)
                throw new ArgumentOutOfRangeException(
                    $"Criteria{index}Rating",
                    $"Rating for criterion {index} must be between 1 and 4 (got {rating}).");
        }

        Check(c1, 1);
        Check(c2, 2);
        Check(c3, 3);
        Check(c4, 4);
        Check(c5, 5);
    }
}

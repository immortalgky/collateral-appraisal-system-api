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
    /// <summary>"Pending" | "Completed"</summary>
    public string EvaluationStatus { get; private set; } = null!;

    public string? EvaluatedBy { get; private set; }
    public DateTime? EvaluatedAt { get; private set; }

    // ── Criterion 1: Report book quality (weight 0.40) ──────────────────────
    public int? Criteria1Rating { get; private set; }

    // ── Criterion 2: Delivery time (weight 0.30, auto-detected) ────────────
    public int? Criteria2Rating { get; private set; }
    public bool Criteria2IsAutoDetected { get; private set; }
    public decimal? Criteria2DetectedDays { get; private set; }

    // ── Criterion 3: Company personnel preparation (weight 0.10) ───────────
    public int? Criteria3Rating { get; private set; }

    // ── Criterion 4: Response time to problem (weight 0.10) ────────────────
    public int? Criteria4Rating { get; private set; }

    // ── Criterion 5: Coordination & responsibility (weight 0.10) ───────────
    public int? Criteria5Rating { get; private set; }

    // ── Free text ───────────────────────────────────────────────────────────
    public string? AdditionalComments { get; private set; }
    public string? Note { get; private set; }

    // ── Private constructor for EF Core ────────────────────────────────────
    private AppraisalEvaluation()
    {
    }

    // ── Factory ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an evaluation for the given appraisal in Pending or Completed state.
    /// Ratings are nullable so a Pending row may hold a partial selection;
    /// Completed requires all five ratings and stamps EvaluatedAt / EvaluatedBy.
    /// </summary>
    public static AppraisalEvaluation Create(
        Guid appraisalId,
        string appraisalNumber,
        string evaluationStatus,
        string? evaluatedBy,
        int? criteria1Rating,
        int? criteria2Rating,
        bool criteria2IsAutoDetected,
        decimal? criteria2DetectedDays,
        int? criteria3Rating,
        int? criteria4Rating,
        int? criteria5Rating,
        string? additionalComments,
        string? note,
        DateTime evaluatedAt)
    {
        ValidateStatus(evaluationStatus);
        ValidateRatings(
            criteria1Rating, criteria2Rating,
            criteria3Rating, criteria4Rating, criteria5Rating);

        if (evaluationStatus == "Completed")
            ValidateAllRatingsSelected(
                criteria1Rating, criteria2Rating,
                criteria3Rating, criteria4Rating, criteria5Rating);

        var evaluation = new AppraisalEvaluation
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            AppraisalNumber = appraisalNumber,
            EvaluationStatus = evaluationStatus,
            Criteria1Rating = criteria1Rating,
            Criteria2Rating = criteria2Rating,
            Criteria2IsAutoDetected = criteria2IsAutoDetected,
            Criteria2DetectedDays = criteria2IsAutoDetected ? criteria2DetectedDays : null,
            Criteria3Rating = criteria3Rating,
            Criteria4Rating = criteria4Rating,
            Criteria5Rating = criteria5Rating,
            AdditionalComments = additionalComments,
            Note = note
        };

        if (evaluationStatus == "Completed")
        {
            evaluation.EvaluatedAt = evaluatedAt;
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
        int? criteria1Rating,
        int? criteria2Rating,
        bool criteria2IsAutoDetected,
        decimal? criteria2DetectedDays,
        int? criteria3Rating,
        int? criteria4Rating,
        int? criteria5Rating,
        string? additionalComments,
        string? note,
        string evaluationStatus,
        string? evaluatedBy,
        DateTime evaluatedAt)
    {
        if (EvaluationStatus == "Completed")
            throw new InvalidOperationException("A completed evaluation cannot be modified.");

        ValidateRatings(
            criteria1Rating, criteria2Rating,
            criteria3Rating, criteria4Rating, criteria5Rating);

        if (evaluationStatus == "Completed")
            ValidateAllRatingsSelected(
                criteria1Rating, criteria2Rating,
                criteria3Rating, criteria4Rating, criteria5Rating);

        Criteria1Rating = criteria1Rating;
        Criteria2Rating = criteria2Rating;
        Criteria2IsAutoDetected = criteria2IsAutoDetected;
        Criteria2DetectedDays = criteria2IsAutoDetected ? criteria2DetectedDays : null;
        Criteria3Rating = criteria3Rating;
        Criteria4Rating = criteria4Rating;
        Criteria5Rating = criteria5Rating;
        AdditionalComments = additionalComments;
        Note = note;
        ValidateStatus(evaluationStatus);
        EvaluationStatus = evaluationStatus;

        if (evaluationStatus == "Completed")
        {
            EvaluatedAt = evaluatedAt;
            EvaluatedBy = evaluatedBy;
        }
    }

    // ── Invariant guards ────────────────────────────────────────────────────

    private static void ValidateStatus(string status)
    {
        if (status is not ("Pending" or "Completed"))
            throw new ArgumentException(
                $"EvaluationStatus must be 'Pending' or 'Completed' (got '{status}').", nameof(status));
    }

    private static void ValidateRatings(
        int? c1, int? c2, int? c3, int? c4, int? c5)
    {
        static void Check(int? rating, int index)
        {
            if (rating.HasValue && rating.Value is < 1 or > 5)
                throw new ArgumentOutOfRangeException(
                    $"Criteria{index}Rating",
                    $"Rating for criterion {index} must be between 1 and 5 (got {rating.Value}).");
        }

        Check(c1, 1);
        Check(c2, 2);
        Check(c3, 3);
        Check(c4, 4);
        Check(c5, 5);
    }

    private static void ValidateAllRatingsSelected(
        int? c1, int? c2, int? c3, int? c4, int? c5)
    {
        if (c1 is null || c2 is null || c3 is null || c4 is null || c5 is null)
            throw new ArgumentException(
                "All five criteria ratings must be selected before marking the evaluation as Completed.");
    }
}

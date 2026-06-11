namespace Collateral.Application.Features.Reappraisal.GetCandidates;

/// <summary>
/// Single row returned by the Reappraisal Candidates list (FSD §3.6.1 columns).
/// </summary>
public class ReappraisalCandidateListItem
{
    public Guid Id { get; set; }
    public string Status { get; set; } = default!;

    /// <summary>ReviewType code (1/2/3). Label resolution deferred to FE/Parameter.</summary>
    public string ReviewType { get; set; } = default!;

    /// <summary>AS400-provided appraisal due date (= ReviewDate field from the file).</summary>
    public DateOnly AppraisalDate { get; set; }

    /// <summary>Days remaining until AppraisalDate (negative = already overdue).</summary>
    public int RemainingDay { get; set; }

    /// <summary>Old Appraisal Report Number (= SurveyNumber = CCSURV).</summary>
    public string OldAppraisalReportNumber { get; set; } = default!;

    /// <summary>CIF number.</summary>
    public string CifNumber { get; set; } = default!;

    /// <summary>CIF name / customer name.</summary>
    public string? CustomerName { get; set; }

    public string CollateralId { get; set; } = default!;
    public string? CollateralName { get; set; }
    public decimal? CurrentValue { get; set; }

    /// <summary>True when a non-terminal reappraisal Appraisal points back at this candidate's prior
    /// appraisal (via PrevAppraisalId). Detected purely from the Appraisal table — no request dependency.</summary>
    public bool HasOpenAppraisal { get; set; }

    /// <summary>The open reappraisal Appraisal's Id.</summary>
    public Guid? OpenAppraisalId { get; set; }

    /// <summary>The open reappraisal Appraisal's AppraisalNumber — shown as "→ &lt;number&gt;" in the badge.</summary>
    public string? OpenAppraisalNumber { get; set; }

    public string? OpenAppraisalGroupTag { get; set; }

    /// <summary>Channel = "SIBS" (bank's code for AS400 — always for this list).</summary>
    public string Channel => "SIBS";
}

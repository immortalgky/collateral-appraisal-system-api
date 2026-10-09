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

    /// <summary>Date of the prior appraisal; NULL when the book cannot be traced anywhere.</summary>
    public DateOnly? AppraisalDate { get; set; }

    /// <summary>Review due date — AS400's EffectiveDateAppraisal (NULL when not sent). The list's due column.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Days remaining until DueDate (negative = already overdue).</summary>
    public int? RemainingDay { get; set; }

    /// <summary>Old Appraisal Report Number (= SurveyNumber = CCSURV).</summary>
    public string OldAppraisalReportNumber { get; set; } = default!;

    /// <summary>CIF number.</summary>
    public string CifNumber { get; set; } = default!;

    /// <summary>CIF name / customer name.</summary>
    public string? CustomerName { get; set; }

    public string CollateralId { get; set; } = default!;
    public string? CollateralName { get; set; }
    public decimal? CurrentValue { get; set; }

    /// <summary>True when a non-terminal reappraisal Appraisal points back at this book, or a
    /// reappraisal Request for it is still waiting to be submitted.</summary>
    public bool HasOpenAppraisal { get; set; }

    /// <summary>The open reappraisal Appraisal's Id.</summary>
    public Guid? OpenAppraisalId { get; set; }

    /// <summary>The open reappraisal Appraisal's AppraisalNumber — shown as "→ &lt;number&gt;" in the badge.</summary>
    public string? OpenAppraisalNumber { get; set; }

    public string? OpenAppraisalGroupTag { get; set; }

    /// <summary>The reappraisal Request created by Initiate and not yet submitted (Draft / New).</summary>
    public Guid? OpenRequestId { get; set; }

    public string? OpenRequestNumber { get; set; }

    /// <summary>Where the prior book lives: <c>CAS</c>, <c>AS400Legacy</c> (a 99A… book) or <c>Unknown</c>.</summary>
    public string PriorAppraisalSource { get; set; } = default!;

    /// <summary>Date of the first AS400 file that listed this book — how long it has been waiting.</summary>
    public DateOnly FirstSeenFileDate { get; set; }

    /// <summary>Date of the latest AS400 file that listed this book.</summary>
    public DateOnly LastSeenFileDate { get; set; }

    // ── Block-project unit: the row reviews one unit of the project (see ReappraisalCandidate.IsBlockUnit) ──
    public bool IsBlockUnit { get; set; }

    /// <summary>Units matched for the row: 1 = found (the Unit* fields below), 0 = none, more = ambiguous.</summary>
    public int? UnitMatchedUnits { get; set; }
    public string? UnitTowerName { get; set; }
    public int? UnitFloor { get; set; }
    public string? UnitRoomNumber { get; set; }
    public string? UnitHouseNumber { get; set; }
    public string? UnitPlotNumber { get; set; }

    // ── Processed tab only: the reappraisal the book produced (see ProcessedBooksSql) ──
    public Guid? NewAppraisalId { get; set; }
    public string? NewAppraisalNumber { get; set; }
    public string? NewAppraisalStatus { get; set; }

    /// <summary>Initiate's group number; NULL when staff raised the request by hand.</summary>
    public string? NewAppraisalGroupTag { get; set; }
    public DateTime? NewAppraisalSubmittedAt { get; set; }
    public DateTime? NewAppraisalCompletedAt { get; set; }

    /// <summary>Channel = "SIBS" (bank's code for AS400 — always for this list).</summary>
    public string Channel => "SIBS";
}

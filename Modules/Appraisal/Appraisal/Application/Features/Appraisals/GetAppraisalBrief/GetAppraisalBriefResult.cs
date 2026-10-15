namespace Appraisal.Application.Features.Appraisals.GetAppraisalBrief;

/// <summary>
/// Everything the credit-side brief screen shows, and nothing else.
///
/// Deliberately a purpose-built shape rather than a reuse of the appraisal DTOs: those carry
/// the internal columns that would then have to be stripped again at every call site. Here
/// the fields simply do not exist, so there is nothing to leak.
/// </summary>
public record GetAppraisalBriefResult
{
    // ── identity ─────────────────────────────────────────────────────────────
    public Guid Id { get; init; }
    public string? AppraisalNumber { get; init; }
    public string? RequestNumber { get; init; }
    public string? LoanApplicationNumber { get; init; }
    public string Status { get; init; } = null!;
    public string? AppraisalType { get; init; }
    public string? Purpose { get; init; }
    public string? Channel { get; init; }
    public string? PropertyTypes { get; init; }

    public string? CustomerName { get; init; }
    public int CustomerCount { get; init; }
    public string? CustomerContactNumber { get; init; }

    public string? RequestedBy { get; init; }
    public string? RequestedByName { get; init; }
    public DateTime? RequestedAt { get; init; }
    public DateTime? AppointmentDateTime { get; init; }
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// The date the appraisal is due — <c>Appraisals.SLADueDate</c>, under a name credit can read.
    ///
    /// It is a target, not a forecast: it is stamped ONCE at intake as
    /// <c>RequestedAt + SlaPolicy.DurationHours</c>, counted in business hours when the policy
    /// says so, and nothing moves it afterwards. So it says "this is when the appraisal team
    /// undertook to be finished", not "this is when it now looks like finishing" — a late job
    /// keeps a date in the past rather than sliding.
    ///
    /// The SLA <i>verdict</i> built on this date (SLAStatus / elapsed / remaining, "Breached",
    /// "AtRisk") stays masked in <see cref="AppraisalFieldScope"/>: how the appraisal team is
    /// performing against its own clock is an internal matter, and a due date on its own does not
    /// disclose it. Once the work is finished, CompletedAt is the answer and the client shows
    /// that instead.
    /// </summary>
    public DateTime? DueDate { get; init; }

    /// <summary>What the customer asked to borrow — the number the appraised value is judged against.</summary>
    public decimal? FacilityLimit { get; init; }

    // ── money: null until IsReleased ─────────────────────────────────────────
    /// <summary>
    /// True once the committee has approved the price. Everything below is null while this is
    /// false, and the client renders the locked state off this single flag rather than
    /// re-deriving the rule from Status.
    /// </summary>
    public bool IsReleased { get; init; }

    /// <summary>
    /// True when the document list below is the real one rather than an empty locked list.
    ///
    /// A LOOSER rule than <see cref="IsReleased"/>, and a separate flag because the client cannot
    /// derive one from the other: the money is withheld from a credit reader until the committee
    /// approves it AND is never shown for cancelled work, while the appraisal folder is withheld
    /// only from a credit reader before approval. An internal caller therefore gets the files on a
    /// cancelled appraisal — they are looking at the same files on the Documents tab — while still
    /// seeing the cancelled notice where the figures would be.
    /// </summary>
    public bool DocumentsReleased { get; init; }

    public decimal? AppraisalValue { get; init; }
    public decimal? ForcedSaleValue { get; init; }
    public decimal? InsuranceValue { get; init; }
    public DateTime? ValuationDate { get; init; }
    public string? ApprovedByCommittee { get; init; }

    /// <summary>
    /// Who is holding the work right now, and how to reach them. Null once nothing is pending —
    /// the appraisal is finished or cancelled and there is nobody to chase.
    /// </summary>
    public BriefHolder? CurrentHolder { get; init; }

    /// <summary>
    /// The committee meeting this appraisal is on the agenda for, when it has reached one.
    ///
    /// The approval step is the one phase where naming a person is the wrong answer — the work is
    /// with a committee, and "SJ Sarah Johnson" says far less than "ขออนุมัติราคาประเมิน ครั้งที่
    /// 53/2569, 6 ส.ค.". Null for an appraisal that has not been tabled, including one approved on
    /// the non-meeting path.
    /// </summary>
    public BriefMeeting? Meeting { get; init; }

    /// <summary>
    /// The valuation firm the work was sent to, with someone credit can actually reach. Null for
    /// an appraisal done in-house.
    /// </summary>
    public BriefExternalCompany? ExternalCompany { get; init; }

    /// <summary>
    /// The appraisal department's admins — the desk to ring when the current holder is not who a
    /// credit officer needs. Same list for every appraisal.
    /// </summary>
    public IReadOnlyList<BriefContact> AppraisalAdmins { get; init; } = [];

    /// <summary>
    /// Set only for a BLOCK appraisal — one that values a whole project rather than a list of
    /// properties. <see cref="Assets"/> is empty in that case, because a block appraisal has no
    /// AppraisalProperties at all; this is its collateral.
    /// </summary>
    public BriefProject? Project { get; init; }

    public IReadOnlyList<BriefAsset> Assets { get; init; } = [];
    public IReadOnlyList<BriefDocument> Documents { get; init; } = [];
}

/// <summary>
/// An outside valuation firm, and who to call there.
///
/// The phase rail used to put the individual appraiser's name under the firm. That is the wrong
/// contact for a credit officer twice over: the appraiser out on a site visit is not who answers,
/// and which of the firm's staff did the work is the appraisal side's business. The firm's own
/// admin is the person who fields "where is this" — every external firm on the dev database has
/// exactly one active ExtAdmin.
/// </summary>
public record BriefExternalCompany(
    string? Name,
    string? NameLocal,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? AdminName,
    string? AdminPhone,
    string? AdminEmail);

/// <summary>
/// A committee meeting an appraisal has been put on the agenda for.
///
/// <c>workflow.MeetingItems</c> is keyed by AppraisalId, so this is a plain lookup. Titles are
/// authored by the secretariat and already read as the committee plus the sitting number
/// ("ขออนุมัติราคาประเมิน ครั้งที่ 53/2569"), so nothing is derived from a code here.
/// </summary>
public record BriefMeeting(string? Title, DateTime? StartAt, string? Status);

/// <summary>
/// The person (or firm) the appraisal is sitting with, and the step they are on.
///
/// This is the one place the panel discloses a staff member's work contact details, and it is
/// deliberately narrow: only the CURRENT holder, never the history. The phase rail already names
/// who handled each earlier step, and a credit officer chasing a job needs to call the person
/// holding it — not everyone who has ever touched it.
///
/// <para>External work is assigned to a FIRM, not a named person: <see cref="AssignedTo"/> is
/// then null and the company fields carry the contact. The client picks whichever side is
/// populated rather than assuming a person is always there.</para>
///
/// <para><see cref="PendingCount"/> exists because a committee step fans out to several
/// reviewers at once (4 requests on the dev database have 3 pending tasks, 4 have 5, one has 6).
/// This record describes the most recently assigned of them; the count tells the reader the work
/// is with a group so the single name is not read as the only person involved.</para>
/// </summary>
public record BriefHolder(
    string? ActivityName,
    string? ActivityId,
    string? AssignedTo,
    string? Name,
    string? Email,
    string? PhoneNumber,
    string? Department,
    string? Position,
    string? CompanyName,
    string? CompanyNameLocal,
    string? CompanyPhone,
    string? CompanyEmail,
    string? CompanyContactPerson,
    DateTime? HeldSince,
    int PendingCount,
    /// <summary>The group a queued task is waiting in, when no individual holds it. Carries the
    /// raw assignee — "IntAdmin", or "ExtAdmin:Team_&lt;companyId&gt;" when PoolAssigneeSelector
    /// scoped it to a team; the client trims the suffix. Null when a person holds the task.</summary>
    string? PoolName = null);

/// <summary>
/// One collateral item.
///
/// <b>There is deliberately no per-item value.</b> <c>AppraisalProperties.SellingPrice</c> looks
/// like the right source but is written on 6 of 105,660 rows on the dev database — pricing in
/// this system hangs off a property GROUP (PricingFinalValues), not off a property. A per-item
/// column here could therefore only ever be blank. Grouped values are a follow-up; the released
/// total on the parent is what the screen compares the facility limit against.
/// </summary>
public record BriefAsset(
    Guid Id,
    int SequenceNumber,
    string? PropertyType,
    string? Title,
    string? Area,
    string? Location,
    /// <summary>Building type code — resolve through the BuildingType parameter group.</summary>
    string? BuildingType,
    decimal? NumberOfFloors,
    /// <summary>
    /// Land area as its three components. Sent raw rather than formatted because the client
    /// totals a group's parcels, and rai-ngan-wa carry at 4 ngan and 100 square wa — a total
    /// cannot be reached by adding display strings.
    /// </summary>
    decimal? AreaRai,
    decimal? AreaNgan,
    decimal? AreaSquareWa,
    /// <summary>A machine's own name — it has no title, area or location to identify it by.</summary>
    string? MachineName,
    /// <summary>
    /// Free text behind <see cref="BuildingType"/> '99' (อื่นๆ). The code alone says only "other",
    /// so for that one value this is the answer and the label is not.
    /// </summary>
    string? BuildingTypeOther,
    /// <summary>Storeys in the condo building this unit is in.</summary>
    decimal? CondoFloors,
    /// <summary>
    /// The floor the unit is on, as stored — nvarchar, and not always a number: the dev database
    /// has '-' where nobody filled it in. Parse defensively.
    /// </summary>
    string? CondoFloorNumber);

/// <summary>
/// The project a block appraisal values.
///
/// Its own shape rather than a synthetic <see cref="BriefAsset"/>: a project is not one property
/// among several, it is the whole subject, and forcing it into the asset list would have meant
/// inventing a property type for it.
/// </summary>
public record BriefProject(
    string? ProjectName,
    /// <summary>Shares the PropertyType wire format — 'U', 'LB', 'L'.</summary>
    string? ProjectType,
    string? Developer,
    int? UnitForSaleCount,
    decimal? LandAreaRai,
    decimal? LandAreaNgan,
    decimal? LandAreaSquareWa,
    string? SubDistrict,
    int? NumberOfPhase,
    /// <summary>Towers in the project. 0 for a horizontal project (houses, shophouses).</summary>
    int TowerCount,
    /// <summary>Storeys in the tallest tower, or null when the project records none.</summary>
    int? MaxFloor,
    /// <summary>Units actually uploaded. Stands in for UnitForSaleCount, which is often unset.</summary>
    int UnitCount,
    /// <summary>
    /// Storeys of a house on a horizontal project — NOT the floor a condo unit is on.
    /// <c>int?</c>, because <c>ProjectUnits.NumberOfFloors</c> is an int column. Dapper matches a
    /// positional record's constructor against the reader's types and rejects the whole
    /// constructor on one mismatch, so a decimal here failed the entire materialisation.
    /// </summary>
    int? UnitStoreys);

/// <summary>
/// A person to contact. Deliberately minimal: a name and the two ways to reach them.
/// <see cref="Department"/> rides along because IntAdmin is not scoped to one desk.
/// </summary>
/// <summary>A person on the appraisal desk. <c>UserName</c> is the bank code they log in with
/// (AspNetUsers.UserName, e.g. "P5229") — credit asks for people by that as often as by name, and
/// two staff can share a display name. Appended last: Dapper binds this record positionally.</summary>
public record BriefContact(
    string? Name,
    string? Email,
    string? PhoneNumber,
    string? Department,
    string? UserName);

/// <summary>
/// A file in the appraisal folder. <see cref="IsPrimary"/> marks the two the credit officer
/// actually files — D043 Appraisal Summary and D001 Appraisal Report — so the client can lead
/// with them and keep the rest at a quieter weight.
/// </summary>
public record BriefDocument(
    Guid DocumentId,
    string TypeCode,
    string? TypeName,
    string? TypeNameTh,
    bool IsPrimary,
    string? FileName,
    long? FileSizeBytes,
    DateTime? UploadedAt);

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

    /// <summary>
    /// The code the appraisal summary is filed under: D042 for a construction progress inspection,
    /// D043 otherwise. Sent whatever the release state: it is a label, not a figure.
    /// </summary>
    public string SummaryTypeCode { get; init; } = "";

    public string? SummaryTypeName { get; init; }

    public string? SummaryTypeNameTh { get; init; }

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

    /// <summary>
    /// The appraisal's machines (see <see cref="BriefMachine"/> for the order). Each one is also an
    /// entry in <see cref="Assets"/>.
    /// </summary>
    public IReadOnlyList<BriefMachine> Machines { get; init; } = [];

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
    /// <summary>Storeys of the building; null when not entered (the form stores 0), as the report omits it.</summary>
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
    /// <summary>Storeys in the condo building this unit is in; null when not entered (0).</summary>
    decimal? CondoFloors,
    /// <summary>
    /// The floor the unit is on — nvarchar, so not always a number ("G" is possible). Trimmed, and null
    /// when blank, dashes only or a numeric zero (the dev database has '-' where nobody filled it in);
    /// the report prints a floor only when it is stated, and the value is otherwise sent as it prints
    /// (trimmed, not dash-stripped: "5 -" stays "5 -"). Still parse defensively.
    /// </summary>
    string? CondoFloorNumber,
    // ── descriptive detail for the 3D collateral scene ───────────────────────────────────────
    // Every field below is null (or an empty list) when the property has no such detail. The
    // codes are the raw parameter codes; the client resolves labels.
    /// <summary>LandShape code. 04-08 have a drawable shape; 01-03 and 99 do not.</summary>
    string? LandShapeType,
    /// <summary>Metres of the parcel that front the road.</summary>
    decimal? RoadFrontage,
    /// <summary>Centimetres the land sits above or below the road.</summary>
    decimal? SoilLevel,
    string? LandFillType,
    /// <summary>LandUse codes. Never null.</summary>
    IReadOnlyList<string> LandUseType,
    /// <summary>No unit is recorded for either pond figure.</summary>
    decimal? PondArea,
    decimal? PondDepth,
    /// <summary>
    /// TRUE MEANS FAR from electricity. The column is still called <c>HasElectricity</c>, but the
    /// screen labels it "Far from Electricity" — renamed here so the wire name says what the value
    /// means.
    /// </summary>
    bool? FarFromElectricity,
    decimal? ElectricityDistance,
    /// <summary>Land's own width, else the condo's.</summary>
    decimal? AccessRoadWidth,
    /// <summary>RoadSurface code — land's own, else the condo's.</summary>
    string? RoadSurfaceType,
    /// <summary>Only the sides that have text or a number.</summary>
    IReadOnlyList<BriefBoundary> Boundaries,
    bool? IsLandlocked,
    bool? IsExpropriated,
    bool? IsInExpropriationLine,
    bool? IsEncroached,
    bool? IsForestBoundary,
    decimal? TotalBuildingArea,
    /// <summary>Roof MATERIAL codes (multi-select). Never null.</summary>
    IReadOnlyList<string> RoofType,
    /// <summary>Fence codes. Never null.</summary>
    IReadOnlyList<string> FenceType,
    /// <summary>The building's own flag, else the condo's.</summary>
    bool? IsUnderConstruction,
    /// <summary>
    /// A property whose only building content is ส่วนพัฒนา (fences, paving …): no house number and
    /// every depreciation row is a non-building one. Same rule as the appraisal summary report.
    /// </summary>
    bool IsDevelopmentOnly,
    /// <summary>ส่วนพัฒนา rows (depreciation rows with IsBuilding = 0). Never null.</summary>
    IReadOnlyList<BriefImprovement> Improvements,
    decimal? UsableArea,
    /// <summary>Facilities codes. Never null.</summary>
    IReadOnlyList<string> FacilityType,
    /// <summary>LocationView codes. Never null.</summary>
    IReadOnlyList<string> LocationViewType)
{
    // ── raw fields behind the appraisal summary's property description ───────────────────────
    // Init properties rather than constructor parameters: they were added after the constructor above
    // was in use, and a new parameter would change every call site. The handler builds this record in
    // C# from name-bound rows, so nothing about Dapper constrains where a property goes. Zero and blank
    // are folded to null where the summary report omits them (the dev database stores 0 for "not
    // entered"). No appraised price lives here; the only prices are the two government rates.

    /// <summary>Every land title of the property, in entry order. Empty for non-land assets.</summary>
    public IReadOnlyList<BriefLandTitle> LandTitles { get; init; } = [];

    /// <summary>
    /// The DEED address of the land or condo, as names (the stored column is a geocode), resolved
    /// through the Title masters; null when the code has no master row. The report's title clause
    /// prints this one.
    /// </summary>
    public string? DeedSubDistrictName { get; init; }
    public string? DeedDistrictName { get; init; }
    public string? DeedProvinceName { get; init; }

    /// <summary>
    /// The property's location through the DOPA masters — what the report prints as ที่ตั้งทรัพย์สิน.
    /// Can differ from the deed address.
    /// </summary>
    public string? DopaSubDistrictName { get; init; }
    public string? DopaDistrictName { get; init; }
    public string? DopaProvinceName { get; init; }

    /// <summary>
    /// How the building names itself: its type label, or the free text behind type '99', else the
    /// property's own name. Same pick as the report's BuildingDisplayName.
    /// </summary>
    public string? BuildingDisplayName { get; init; }

    /// <summary>Building's or condo's age in years; null when 0 (not entered).</summary>
    public int? BuildingAge { get; init; }

    /// <summary>Building's or condo's condition as a Thai label (code '99' resolves to its remark).</summary>
    public string? BuildingCondition { get; init; }

    /// <summary>
    /// 0 up to (not including) 100 while an inspection says work is under way; null when there is
    /// no inspection or it is complete — the report prints no progress clause for either.
    /// </summary>
    public decimal? ConstructionProgressPercent { get; init; }

    // condo only
    public string? RoomNumber { get; init; }

    /// <summary>Null when blank or dashes only.</summary>
    public string? BuildingNumber { get; init; }
    public string? CondoName { get; init; }
    public string? CondoRegistrationNumber { get; init; }

    /// <summary>The title the condo stands on: its title number, else the legacy BuiltOnTitleNumber.</summary>
    public string? BuiltOnTitleNumber { get; init; }

    /// <summary>The unit's area components (room, balcony …), positive sizes only. Empty when none.</summary>
    public IReadOnlyList<BriefCondoArea> CondoAreas { get; init; } = [];

    /// <summary>Government rate per square metre, as stored.</summary>
    public decimal? GovernmentPricePerSqm { get; init; }

    /// <summary>Appraiser's ตกสำรวจ flag — the report prints that word instead of the rate.</summary>
    public bool? IsMissingFromSurvey { get; init; }

    /// <summary>
    /// The free text behind code '99' (อื่นๆ) of the matching code list, so the client can show what
    /// the appraiser typed. Null when none was entered.
    /// </summary>
    public string? LandUseTypeOther { get; init; }
    public string? RoofTypeOther { get; init; }
    public string? FenceTypeOther { get; init; }
    public string? FacilityTypeOther { get; init; }
    public string? LocationViewTypeOther { get; init; }

    /// <summary>The free text behind road surface code '99', land's own else the condo's.</summary>
    public string? RoadSurfaceTypeOther { get; init; }

    /// <summary>The free text behind land fill code '99' (land's own else the condo's) and land shape code '99' (land only).</summary>
    public string? LandFillTypeOther { get; init; }
    public string? LandShapeTypeOther { get; init; }
}

/// <summary>
/// One land title (deed). <see cref="TitleTypeLabel"/> and <see cref="TitleNumberPrefix"/> follow the
/// report's TitleDeedLabel: the document's own name rather than a fixed "โฉนดที่ดิน", with the
/// spacing before "เลขที่" the report uses. Area is sent as components for the same reason as
/// <see cref="BriefAsset.AreaRai"/>.
/// </summary>
public record BriefLandTitle(
    string? TitleType,
    string TitleTypeLabel,
    string TitleNumberPrefix,
    string? TitleNumber,
    string? BookNumber,
    string? PageNumber,
    string? LandParcelNumber,
    string? SurveyNumber,
    string? Rawang,
    decimal? AreaRai,
    decimal? AreaNgan,
    decimal? AreaSquareWa,
    /// <summary>The government rate per square wa, as stored (the report prints a stored 0 too).</summary>
    decimal? GovernmentPricePerSqWa,
    bool IsMissingFromSurvey);

/// <summary>One area component of a condo unit; <see cref="Description"/> is the appraiser's own label.</summary>
public record BriefCondoArea(string? Description, decimal Size);

/// <summary>
/// One machine, with the fields the summary report describes it by. The report prints machines in
/// sections per property group; <see cref="Section"/> is the section. Machines in a group follow the
/// report's order (group, section, then the appraiser's sequence); machines in no group come last —
/// the report would not print them, but the brief is read before grouping is finished.
/// </summary>
/// <param name="AssetId">The <see cref="BriefAsset.Id"/> of the property this machine is.</param>
/// <param name="GroupNumber">Property group, null when the machine is in none.</param>
/// <param name="Section">'registered' | 'notRegistered' | 'notInstalled'.</param>
/// <param name="ConditionUse">'03' = surveyed but not found.</param>
/// <param name="IsPriceCertified">Part of the valuation outcome, so withheld (null) under the same
/// condition as the appraised value.</param>
/// <param name="InstallationStatus">'2' = under procurement, which is what puts it in 'notInstalled'.</param>
public record BriefMachine(
    Guid AssetId,
    int? GroupNumber,
    string Section,
    string? MachineName,
    string? Brand,
    string? Model,
    string? RegistrationNumber,
    string? SerialNumber,
    int? YearOfManufacture,
    string? MachineCondition,
    string? ConditionUse,
    bool? IsPriceCertified,
    bool RegistrationStatus,
    string? InstallationStatus);

/// <summary>
/// One side of a land parcel. <see cref="Distance"/> is how far the boundary is from what the side
/// adjoins (<see cref="AdjacentArea"/>) — NOT the length of the side.
/// </summary>
/// <param name="Direction">'north' | 'south' | 'east' | 'west'.</param>
public record BriefBoundary(string Direction, string? AdjacentArea, decimal? Distance);

/// <summary>A ส่วนพัฒนา row: what it is (free text) and how big (m²).</summary>
public record BriefImprovement(string? Description, decimal? Area);

/// <summary>The land a project stands on. Same meanings as the matching <see cref="BriefAsset"/> fields.</summary>
public record BriefProjectLand(
    string? LandShapeType,
    decimal? RoadFrontage,
    decimal? AccessRoadWidth,
    string? RoadSurfaceType,
    IReadOnlyList<string> LandUseType,
    bool? IsLandlocked,
    bool? IsExpropriated,
    bool? IsInExpropriationLine,
    bool? IsEncroached,
    bool? IsForestBoundary)
{
    /// <summary>The free text behind land use / road surface code '99'.</summary>
    public string? LandUseTypeOther { get; init; }
    public string? RoadSurfaceTypeOther { get; init; }
}

/// <summary>
/// A house design on a horizontal project. <see cref="StandardLandArea"/> is square wa;
/// <see cref="Units"/> and <see cref="SoldUnits"/> count the plots built to this model.
/// </summary>
public record BriefHouseModel(
    Guid Id,
    string? ModelName,
    string? BuildingType,
    string? BuildingTypeOther,
    decimal? NumberOfFloors,
    decimal? StandardLandArea,
    decimal? StandardUsableArea,
    IReadOnlyList<string> RoofType,
    IReadOnlyList<string> FenceType,
    int Units,
    int SoldUnits)
{
    /// <summary>Houses of this model; null when not entered (0).</summary>
    public int? NumberOfHouse { get; init; }

    /// <summary>
    /// The smaller end of the model's usable-area range. The report prints
    /// <c>StandardUsableArea ?? UsableAreaMin</c>, so a model with no standard area is described by this.
    /// </summary>
    public decimal? UsableAreaMin { get; init; }

    /// <summary>The free text behind roof / fence code '99'.</summary>
    public string? RoofTypeOther { get; init; }
    public string? FenceTypeOther { get; init; }

    /// <summary>The building type's Thai label, or the free text behind type '99'.</summary>
    public string? BuildingTypeDisplay { get; init; }
}

/// <summary>
/// One plot (a ProjectUnit) of a horizontal project. <see cref="LandArea"/> is square wa. Dash
/// placeholders in the plot and house numbers are dropped, and 0 floors / areas are null.
/// </summary>
public record BriefPlot(
    string? PlotNumber,
    string? HouseNumber,
    Guid? ModelId,
    string? ModelType,
    int? NumberOfFloors,
    decimal? LandArea,
    decimal? UsableArea,
    bool IsSold);

/// <summary>
/// One tower of a condo project, with its floors. <see cref="NumberOfFloors"/> is the recorded
/// height and is null whenever nobody recorded one — it is never inferred from the unit floors.
/// </summary>
public record BriefTower(
    string? TowerName,
    int? NumberOfFloors,
    int? NumberOfUnits,
    IReadOnlyList<BriefTowerFloor> Floors);

/// <summary>Units on one floor of a tower. <see cref="Floor"/> is null for units uploaded without one.</summary>
public record BriefTowerFloor(int? Floor, int Units, int SoldUnits);

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
    /// <c>int?</c>, because <c>ProjectUnits.NumberOfFloors</c> is an int column.
    /// </summary>
    int? UnitStoreys)
{
    /// <summary>
    /// Not a constructor parameter on purpose: towers come from their own result sets. Empty for a
    /// horizontal project.
    /// </summary>
    public IReadOnlyList<BriefTower> Towers { get; init; } = [];

    /// <summary>
    /// Units uploaded with no tower (<c>ProjectTowerId</c> NULL). Not a tower, so not in
    /// <see cref="Towers"/>: the uploaded units of all towers (the sum of their floors' <c>Units</c>)
    /// plus this equal <see cref="UnitCount"/>. A tower's own <c>NumberOfUnits</c> is the recorded
    /// figure and need not add up. For a horizontal project that is every unit.
    /// </summary>
    public int UnassignedUnits { get; init; }

    /// <summary>Of <see cref="UnassignedUnits"/>, how many are sold.</summary>
    public int UnassignedSoldUnits { get; init; }

    // ── project scene detail ─────────────────────────────────────────────────────────────────
    // Init properties for the same reason as Towers: the constructor stays the original thirteen.
    public bool? IsUnderConstruction { get; init; }

    /// <summary>
    /// Percent complete while work is under way: 0 means not started, and null means no figure — either
    /// not under construction (<see cref="IsUnderConstruction"/> is not true) or finished (100 or more).
    /// Not the same gate as <see cref="BriefAsset.ConstructionProgressPercent"/>: that one follows the
    /// property's construction inspection (as the report's RS24 does) and does not look at an
    /// under-construction flag; both clamp 100 or more to null and keep 0 as "not started".
    /// </summary>
    public decimal? ConstructionProgressPercent { get; init; }

    /// <summary>Facilities codes. Never null.</summary>
    public IReadOnlyList<string> Facilities { get; init; } = [];

    // ── raw fields behind the appraisal summary's project description ────────────────────────
    public string? HouseNumber { get; init; }
    public string? Soi { get; init; }
    public string? Road { get; init; }

    /// <summary>
    /// Location as NAMES. DOPA masters first, then the Title masters for legacy projects that hold Title
    /// codes; null when neither knows the code — a code is never sent as a name.
    /// </summary>
    public string? SubDistrictName { get; init; }
    public string? DistrictName { get; init; }
    public string? ProvinceName { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

    /// <summary>Partial Gregorian date as typed: "YYYY", "YYYY-MM" or "YYYY-MM-DD".</summary>
    public string? ProjectSaleLaunchDate { get; init; }

    /// <summary>PublicUtility labels (an unmapped code stays as the code). Never null.</summary>
    public IReadOnlyList<string> Utilities { get; init; } = [];
    public string? UtilitiesOther { get; init; }

    /// <summary>Facilities as labels — <see cref="Facilities"/> holds the codes. Never null.</summary>
    public IReadOnlyList<string> FacilityLabels { get; init; } = [];
    public string? FacilitiesOther { get; init; }

    /// <summary>The project's land, or null when none was recorded.</summary>
    public BriefProjectLand? Land { get; init; }

    /// <summary>House models of a horizontal (LB or L) project; empty for any other project. An L project (land only) may have none.</summary>
    public IReadOnlyList<BriefHouseModel> HouseModels { get; init; } = [];

    /// <summary>
    /// Plots of a horizontal (LB or L) project in upload order (batch time, then the sheet's own
    /// numbering), capped at 500; empty for any other project. <see cref="UnitCount"/> is the
    /// uncapped count.
    /// </summary>
    public IReadOnlyList<BriefPlot> Plots { get; init; } = [];
}

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
/// actually files — the Appraisal Summary (D043, or D042 for a construction progress inspection) and
/// D001 Appraisal Report — so the client can lead with them and keep the rest at a quieter weight.
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

namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "รายละเอียดที่ดิน" (Land Details) section — FSD §2.1.2.4.
///
/// All scalar properties are nullable so that partial data renders as blank.
/// The section is absent from the report when <see cref="LandSectionLoader.LoadAsync"/>
/// returns <see langword="null"/> (appraisal has no land properties).
/// </summary>
public sealed class LandSection
{
    // ── Grouping (appraisal-book group-major layout; not displayed by the partial) ──

    /// <summary>กลุ่มที่ — PropertyGroups.GroupNumber for this property (0 = ungrouped).</summary>
    public int GroupNumber { get; init; }

    /// <summary>Group label — PropertyGroups.GroupName (null when ungrouped).</summary>
    public string? GroupName { get; init; }

    // ── Header / area summary ─────────────────────────────────────────────────────

    // ── รวมเนื้อที่ดิน — total area across this property's titles (rendered as a table
    //    total row). Normalised via ThaiLandAreaFormatter (100 sq wa = 1 ngan, 4 ngan = 1 rai).

    /// <summary>รวมเนื้อที่ดิน (ไร่) — normalised carried sum.</summary>
    public int TotalRai { get; init; }

    /// <summary>รวมเนื้อที่ดิน (งาน) — normalised carried sum.</summary>
    public int TotalNgan { get; init; }

    /// <summary>รวมเนื้อที่ดิน (ตร.วา) — normalised carried sum.</summary>
    public int TotalSquareWa { get; init; }

    /// <summary>รวมทั้งหมดเป็นตารางวา ("หรือ X ตารางวา").</summary>
    public decimal TotalAreaInWa { get; init; }

    /// <summary>
    /// ตรวจสอบจาก — land check-method, resolved from LandAppraisalDetails.LandCheckMethodType
    /// (parameter group 'CheckBy'); falls back to LandCheckMethodTypeOther free-text.
    /// </summary>
    public string? CheckedFrom { get; init; }

    // ── Address ────────────────────────────────────────────────────────────────────

    /// <summary>ตำบล / แขวง — source: LandAppraisalDetails.SubDistrict (Address VO).</summary>
    public string? SubDistrict { get; init; }

    /// <summary>อำเภอ / เขต — source: LandAppraisalDetails.District (Address VO).</summary>
    public string? District { get; init; }

    /// <summary>จังหวัด — source: LandAppraisalDetails.Province (Address VO).</summary>
    public string? Province { get; init; }

    /// <summary>สำนักงานที่ดิน — source: LandAppraisalDetails.LandOffice (Address VO).</summary>
    public string? LandOffice { get; init; }

    // ── Obligation (ภาระผูกพัน) ───────────────────────────────────────────────────

    /// <summary>
    /// ภาระผูกพัน — source: LandAppraisalDetails.ObligationDetails.
    /// </summary>
    public string? Obligation { get; init; }

    // ── Road / transportation (การคมนาคม) ────────────────────────────────────────

    /// <summary>
    /// ถนนผ่านหน้า — source: LandAppraisalDetails.RoadPassInFrontOfLand.
    /// </summary>
    public string? RoadPassInFrontOfLand { get; init; }

    /// <summary>
    /// ลักษณะผิวจราจร — source: LandAppraisalDetails.RoadSurfaceType, resolved to its
    /// Thai description (parameter group 'RoadSurface') by LandSectionLoader.
    /// </summary>
    public string? RoadSurfaceType { get; init; }

    /// <summary>
    /// เขตทางกว้าง (metres) — source: LandAppraisalDetails.AccessRoadWidth.
    /// </summary>
    public decimal? AccessRoadWidth { get; init; }

    /// <summary>
    /// ผิวจราจรกว้าง (metres) — source: LandAppraisalDetails.RoadFrontage.
    /// </summary>
    public decimal? RoadFrontage { get; init; }

    // ── Access / land condition / utilities (ทางเข้าออกที่ดิน) ───────────────────

    /// <summary>
    /// สิทธิทางเข้าออก (comma-joined display) — source: LandAppraisalDetails.LandEntranceExitType
    /// (JSON array decoded by LandSectionLoader).
    /// </summary>
    public string? LandEntranceExitType { get; init; }

    /// <summary>
    /// สภาพที่ดิน (comma-joined display) — source: LandAppraisalDetails.PlotLocationType
    /// (JSON array decoded by LandSectionLoader).
    /// </summary>
    public string? PlotLocationType { get; init; }

    /// <summary>
    /// สาธารณูปโภค (comma-joined display) — source: LandAppraisalDetails.PublicUtilityType
    /// (JSON array decoded by LandSectionLoader).
    /// </summary>
    public string? PublicUtilityType { get; init; }

    // ── Physical characteristics (ลักษณะของที่ดิน) ───────────────────────────────

    /// <summary>
    /// ลักษณะทางกายภาพ — source: LandAppraisalDetails.LandDescription (free-text).
    /// </summary>
    public string? LandDescription { get; init; }

    /// <summary>
    /// การใช้ประโยชน์ที่ดิน (comma-joined display) — source: LandAppraisalDetails.LandUseType
    /// (JSON array decoded by LandSectionLoader).
    /// </summary>
    public string? LandUseType { get; init; }

    // ── Environment (สภาพแวดล้อม) ─────────────────────────────────────────────────

    /// <summary>
    /// ทรัพย์สินอยู่ในพื้นที่ (transportation access context, comma-joined display) —
    /// source: LandAppraisalDetails.TransportationAccessType (JSON array decoded by LandSectionLoader).
    /// </summary>
    public string? TransportationAccessType { get; init; }

    /// <summary>
    /// สภาพแวดล้อม (anticipated property type, comma-joined display) —
    /// source: LandAppraisalDetails.PropertyAnticipationType.
    /// </summary>
    public string? PropertyAnticipationType { get; init; }

    // ── Legal restrictions (ข้อกำหนดทางกฎหมาย) ──────────────────────────────────

    /// <summary>
    /// อยู่ในแนวเวนคืน checkbox — source: LandAppraisalDetails.IsExpropriated (bool column).
    /// Null when the column has no data for this property.
    /// </summary>
    public bool? IsInExpropriationLine { get; init; }

    // NOTE: no source — LandAppraisalDetailConfiguration has no `IsInExpropriationLine` boolean
    // column distinct from IsExpropriated. Mapped from IsExpropriated above.

    /// <summary>
    /// พื้นที่สี (urban-planning colour zone) — source: LandAppraisalDetails.UrbanPlanningType.
    /// </summary>
    public string? UrbanPlanningType { get; init; }

    /// <summary>
    /// ที่ดินประเภท (comma-joined display) — source: LandAppraisalDetails.LandZoneType
    /// (JSON array decoded by LandSectionLoader).
    /// </summary>
    public string? LandZoneType { get; init; }

    // ── Remark ────────────────────────────────────────────────────────────────────

    /// <summary>หมายเหตุ — source: LandAppraisalDetails.Remark.</summary>
    public string? Remark { get; init; }

    // ── Title deed table ──────────────────────────────────────────────────────────

    /// <summary>
    /// Rows for the title-deed table (โฉนดที่ดิน / น.ส. / ฯลฯ).
    /// Ordered by (AppraisalProperty.SequenceNumber, LandTitle.Id).
    /// </summary>
    public IReadOnlyList<LandTitleRow> Titles { get; init; } = [];
}

/// <summary>
/// One row in the title-deed table (ตารางเอกสารสิทธิ์).
///
/// Source table: appraisal.LandTitles (OwnsMany on LandAppraisalDetail).
/// OwnerName is inherited from the parent LandAppraisalDetail row because
/// LandTitles does not carry its own OwnerName column.
/// </summary>
public sealed class LandTitleRow
{
    /// <summary>ลำดับ — 1-based row number within the section.</summary>
    public int Sequence { get; init; }

    /// <summary>โฉนดเลขที่ — source: LandTitles.TitleNumber.</summary>
    public string? TitleNumber { get; init; }

    /// <summary>เลขที่ดิน — source: LandTitles.LandParcelNumber.</summary>
    public string? LandParcelNumber { get; init; }

    /// <summary>หน้าสำรวจ — source: LandTitles.SurveyNumber.</summary>
    public string? SurveyNumber { get; init; }

    /// <summary>ระวาง — source: LandTitles.MapSheetNumber + Rawang (space-joined).</summary>
    public string? MapSheet { get; init; }

    /// <summary>เนื้อที่ดิน (ไร่) — source: LandTitles.Area.Rai (column AreaRai).</summary>
    public decimal? AreaRai { get; init; }

    /// <summary>เนื้อที่ดิน (งาน) — source: LandTitles.Area.Ngan (column AreaNgan).</summary>
    public decimal? AreaNgan { get; init; }

    /// <summary>เนื้อที่ดิน (ตร.วา) — source: LandTitles.Area.SquareWa (column AreaSquareWa).</summary>
    public decimal? AreaSquareWa { get; init; }

    /// <summary>
    /// ผู้ถือกรรมสิทธิ์ — inherited from parent LandAppraisalDetails.OwnerName
    /// (LandTitles table has no OwnerName column of its own).
    /// </summary>
    public string? OwnerName { get; init; }
}

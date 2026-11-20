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

    /// <summary>
    /// รวมเนื้อที่ดิน (ตร.วา) — normalised carried sum. Decimal, not int: a deed records square wa to
    /// two places and this row is reconciled against the deed.
    /// </summary>
    public decimal TotalSquareWa { get; init; }

    /// <summary>รวมทั้งหมดเป็นตารางวา ("หรือ X ตารางวา").</summary>
    public decimal TotalAreaInWa { get; init; }

    /// <summary>หัก เนื้อที่ที่ตัดออกจากการประเมิน (ตร.ว.) — 0 when nothing was deducted.</summary>
    public decimal DeductedAreaInWa { get; init; }

    /// <summary>คงเหลือ เนื้อที่ที่ใช้ประเมิน (ตร.ว.) — what the pricing actually used.</summary>
    public decimal NetAreaInWa { get; init; }

    /// <summary>การรอนสิทธิ์ — LandAppraisalDetails.EvictionType (group 'Eviction'; 99 prints EvictionTypeOther), joined ", ". Null when none.</summary>
    public string? EvictionType { get; init; }

    /// <summary>รายละเอียดการรอนสิทธิ์ — LandAppraisalDetails.EvictionRemark, printed as the remark line under the การรอนสิทธิ์ row.</summary>
    public string? EvictionRemark { get; init; }

    /// <summary>The land's area deductions (LandAreaDeductions), in the appraiser's order; empty when there are none.</summary>
    public IReadOnlyList<LandDeductionRow> Deductions { get; init; } = [];

    /// <summary>ระวางแผนที่ — the distinct ระวาง (LandTitles.Rawang) of this property's titles, comma-joined.</summary>
    public string? MapSheets { get; init; }

    /// <summary>
    /// ตรวจสอบจาก — land check-method, resolved from LandAppraisalDetails.LandCheckMethodType
    /// (parameter group 'CheckBy'); falls back to LandCheckMethodTypeOther free-text.
    /// </summary>
    public string? CheckedFrom { get; init; }

    /// <summary>ตำแหน่งที่ดินถูกต้อง (ผลตรวจตำแหน่ง) — source: LandAppraisalDetails.IsLandLocationVerified.</summary>
    public bool? IsLandLocationVerified { get; init; }

    /// <summary>หมายเหตุตำแหน่งที่ดิน — LandAppraisalDetails.LandLocationRemark; printed only when the position is marked ไม่ถูกต้อง.</summary>
    public string? LandLocationRemark { get; init; }

    /// <summary>ตรวจสอบกรรมสิทธิ์แล้ว — source: LandAppraisalDetails.IsOwnerVerified.</summary>
    public bool? IsOwnerVerified { get; init; }

    /// <summary>การให้เช่า — source: LandAppraisalDetails.IsRentedOut.</summary>
    public bool? IsRentedOut { get; init; }

    /// <summary>พิกัด GPS — source: LandAppraisalDetails.Latitude / Longitude (Coordinates VO).</summary>
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

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

    /// <summary>ภาระผูกพันการจำนอง — source: ObligationType (02) + MortgageDetails; "ไม่มี" when not mortgaged.</summary>
    public string? ObligationMortgage { get; init; }

    /// <summary>ภาระผูกพันอื่นๆ — source: ObligationType (03/04/05/99) + ObligationDetails; "ไม่มี" when none.</summary>
    public string? ObligationOthers { get; init; }

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

    /// <summary>ผิวจราจรกว้าง (metres) — source: LandAppraisalDetails.CarriagewayWidth.</summary>
    public decimal? CarriagewayWidth { get; init; }

    /// <summary>เขตทางกว้าง (metres) — source: LandAppraisalDetails.RightOfWayWidth.</summary>
    public decimal? RightOfWayWidth { get; init; }

    /// <summary>
    /// หน้าที่ดินติดถนนกว้าง (metres) — source: LandAppraisalDetails.RoadFrontage.
    /// </summary>
    public decimal? RoadFrontage { get; init; }

    /// <summary>จำนวนช่องจราจร (เลน) — source: LandAppraisalDetails.NumberOfLanes.</summary>
    public short? NumberOfLanes { get; init; }

    /// <summary>จำนวนด้านที่ติดถนน — source: LandAppraisalDetails.NumberOfSidesFacingRoad.</summary>
    public int? NumberOfSidesFacingRoad { get; init; }

    /// <summary>ห่างจากถนนใหญ่ (metres) — source: LandAppraisalDetails.DistanceFromMainRoad.</summary>
    public decimal? DistanceFromMainRoad { get; init; }

    /// <summary>การคมนาคมขนส่ง — LandAppraisalDetails.TransportationAccessType (parameter group 'Transportation').</summary>
    public string? TransportationAccessType { get; init; }

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
    /// รูปแปลงที่ดิน — source: LandAppraisalDetails.LandShapeType (code, group 'LandShape'), resolved to its Thai description.
    /// </summary>
    public string? LandDescription { get; init; }

    /// <summary>ความเหมาะสมต่อการพัฒนา — LandAppraisalDetails.DevelopmentSuitabilityType (code, group 'DevelopmentSuitability').</summary>
    public string? DevelopmentSuitability { get; init; }

    /// <summary>ลักษณะทางกายภาพ (free text) — LandAppraisalDetails.LandDescription. <see cref="LandDescription"/> holds the LandShapeType dropdown instead.</summary>
    public string? LandDescriptionText { get; init; }

    /// <summary>
    /// การใช้ประโยชน์ที่ดิน (comma-joined display) — source: LandAppraisalDetails.LandUseType
    /// (JSON array decoded by LandSectionLoader).
    /// </summary>
    public string? LandUseType { get; init; }

    // ── Environment (สภาพแวดล้อม) ─────────────────────────────────────────────────

    /// <summary>
    /// การคาดการณ์ความเจริญ (แนวโน้มความเจริญ) — source: LandAppraisalDetails.PropertyAnticipationType
    /// (parameter group 'AnticipationOfProsperity').
    /// </summary>
    public string? PropertyAnticipationType { get; init; }

    // ── Legal restrictions (ข้อกำหนดทางกฎหมาย) ──────────────────────────────────

    /// <summary>
    /// อยู่ในแนวเวนคืน checkbox — source: LandAppraisalDetails.IsInExpropriationLine (bool column; the legacy
    /// IsExpropriated "ระวางเวนคืน" is not read). Null when the column has no data for this property.
    /// </summary>
    public bool? IsInExpropriationLine { get; init; }

    /// <summary>
    /// ผังเมือง (ประเภทย่าน + ความหนาแน่น) — source: LandAppraisalDetails.UrbanPlanningType
    /// (parameter group 'TypeOfUrbanPlanning'). Printed as ที่ดินประเภท, not as สภาพแวดล้อม (see <see cref="EnvironmentType"/>). It is NOT the
    /// ผังเมือง colour — that is <see cref="UrbanPlanningColor"/>.
    /// </summary>
    public string? UrbanPlanningType { get; init; }

    /// <summary>สภาพแวดล้อม — LandAppraisalDetails.EnvironmentType (JSON codes, group 'Environment'; 99 prints EnvironmentTypeOther), joined ", ". Null on records saved before the field existed.</summary>
    public string? EnvironmentType { get; init; }

    /// <summary>สีผังเมืองตามแผนผังจริง — LandAppraisalDetails.UrbanPlanningColor (group 'UrbanPlanningColor'; 99 prints the Other text).</summary>
    public string? UrbanPlanningColor { get; init; }

    /// <summary>
    /// ทรัพย์สินอยู่ในพื้นที่ (เขตสุขาภิบาล / เทศบาล / อบต. / กทม.) — source: LandAppraisalDetails.LandZoneType
    /// (parameter group 'Location').
    /// </summary>
    public string? LandZoneType { get; init; }

    /// <summary>อยู่ในแนวเขตป่า — LandAppraisalDetails.IsForestBoundary (+ ForestBoundaryRemark).</summary>
    public bool? IsForestBoundary { get; init; }
    public string? ForestBoundaryRemark { get; init; }

    /// <summary>ปัญหาน้ำท่วมขัง (book 1.5) — LandAppraisalDetails.HasFloodProblem (+ FloodProblemRemark).</summary>
    public bool? HasFloodProblem { get; init; }
    public string? FloodProblemRemark { get; init; }

    /// <summary>ปัญหามลพิษ (book 1.5) — HasPollutionProblem; PollutionType is the labels joined " / " (code 99 prints PollutionTypeOther).</summary>
    public bool? HasPollutionProblem { get; init; }
    public string? PollutionType { get; init; }
    public string? PollutionProblemRemark { get; init; }

    /// <summary>ปัญหาอื่นๆ (book 1.5) — HasOtherEnvironmentProblem (+ OtherEnvironmentProblemRemark, printed as the row's value).</summary>
    public bool? HasOtherEnvironmentProblem { get; init; }
    public string? OtherEnvironmentProblemRemark { get; init; }

    /// <summary>ถูกรุกล้ำ — LandAppraisalDetails.IsEncroached (+ EncroachmentRemark).</summary>
    public bool? IsEncroached { get; init; }
    public string? EncroachmentRemark { get; init; }

    /// <summary>ที่ดินตาบอด — LandAppraisalDetails.IsLandlocked (+ LandlockedRemark).</summary>
    public bool? IsLandlocked { get; init; }
    public string? LandlockedRemark { get; init; }

    /// <summary>หมายเหตุแนวเวนคืน — ExpropriationLineRemark only, with the พรฎ./พรบ. in RoyalDecree.</summary>
    public string? ExpropriationRemark { get; init; }
    public string? RoyalDecree { get; init; }

    /// <summary>ข้อจำกัดทางกฎหมายอื่น ๆ — LandAppraisalDetails.OtherLegalLimitations.</summary>
    public string? OtherLegalLimitations { get; init; }

    // ── Plot, access and boundaries (รายละเอียดเพิ่ม) ───────────────────────────────

    /// <summary>สภาพที่ดิน — LandAppraisalDetails.LandFillType (parameter group 'Landfill'; ถมแล้ว / ยังไม่ถม / …).</summary>
    public string? LandFill { get; init; }

    /// <summary>% ที่ถมแล้ว — LandAppraisalDetails.LandFillPercent.</summary>
    public decimal? LandFillPercent { get; init; }

    /// <summary>ระดับที่ดิน (เมตร, เทียบกับถนน) — LandAppraisalDetails.SoilLevel.</summary>
    public decimal? SoilLevel { get; init; }

    /// <summary>เนื้อที่บ่อน้ำ (ตารางวา) — LandAppraisalDetails.PondArea; printed with PondDepth only when one of them is above 0.</summary>
    public decimal? PondArea { get; init; }

    /// <summary>ความลึกบ่อน้ำ (เมตร) — LandAppraisalDetails.PondDepth.</summary>
    public decimal? PondDepth { get; init; }

    /// <summary>การเข้าถึงที่ดิน — LandAccessibilityType resolved through parameter group 'LandAccessibility'.</summary>
    public string? LandAccessibility { get; init; }

    /// <summary>หมายเหตุการเข้าถึงที่ดิน — LandAccessibilityRemark (null when blank).</summary>
    public string? LandAccessibilityRemark { get; init; }

    /// <summary>True when the plot lies far from the power line (the form's "ห่างไฟฟ้า"); the distance is <see cref="ElectricityDistance"/>.</summary>
    public bool? FarFromElectricity { get; init; }

    /// <summary>ระยะห่างจากไฟฟ้า (เมตร) — LandAppraisalDetails.ElectricityDistance.</summary>
    public decimal? ElectricityDistance { get; init; }

    /// <summary>หลักเขตที่พบ — LandTitles.BoundaryMarkerType (parameter group 'BoundaryMarker') + BoundaryMarkerRemark, across this property's titles.</summary>
    public string? BoundaryMarker { get; init; }

    /// <summary>
    /// อาณาเขตและเขตติดต่อ — one row per compass side that has data. The distance is from the boundary to what the
    /// side adjoins, not the side's length (see LandAppraisalDetails.*BoundaryLength).
    /// </summary>
    public IReadOnlyList<LandBoundaryRow> Boundaries { get; init; } = [];

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

/// <summary>One compass side of the land: what it adjoins and the distance to it.</summary>
public sealed class LandBoundaryRow
{
    /// <summary>ทิศเหนือ / ใต้ / ตะวันออก / ตะวันตก.</summary>
    public string Direction { get; init; } = "";

    /// <summary>ติดต่อกับ — free text (ถนน, คลอง, บ้าน, ที่ดินว่าง …).</summary>
    public string? Adjacent { get; init; }

    /// <summary>ระยะ (เมตร) from the boundary to what the side adjoins.</summary>
    public decimal? Distance { get; init; }
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

    /// <summary>
    /// เอกสารสิทธิ์ — the document's Thai name, resolved from LandTitles.TitleType through
    /// <see cref="Reporting.Application.Formatting.TitleDeedLabel"/>. Its own column, because one
    /// section can list parcels of different kinds side by side.
    /// </summary>
    public string? TitleTypeDisplay { get; init; }

    /// <summary>เลขที่ — source: LandTitles.TitleNumber.</summary>
    public string? TitleNumber { get; init; }

    /// <summary>เลขที่ดิน — source: LandTitles.LandParcelNumber.</summary>
    public string? LandParcelNumber { get; init; }

    /// <summary>หน้าสำรวจ — source: LandTitles.SurveyNumber.</summary>
    public string? SurveyNumber { get; init; }

    /// <summary>ระวาง — source: LandTitles.Rawang (the cadastral sheet of a โฉนด).</summary>
    public string? MapSheet { get; init; }

    /// <summary>ผลตรวจสอบเอกสาร — LandTitles.DocumentValidationResultType: "01" ถูกต้องตรงกัน, "02" ไม่สอดคล้องกัน; null for any other value (older rows hold '1' or blank).</summary>
    public string? DocumentValidationResult { get; init; }

    /// <summary>ระวาง — source: LandTitles.Rawang.</summary>
    public string? Rawang { get; init; }

    /// <summary>ราคาประเมินราชการ (บาท/ตร.ว.) — source: LandTitles.GovernmentPricePerSqWa.</summary>
    public decimal? GovernmentPricePerSqWa { get; init; }

    /// <summary>ราคาประเมินราชการรวม (บาท) — source: LandTitles.GovernmentPrice.</summary>
    public decimal? GovernmentPrice { get; init; }

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

/// <summary>One line of the 2.2 deduction table: the reason (99 prints its own text), the area cut in sq wa, and the remark.</summary>
public sealed class LandDeductionRow
{
    public int Sequence { get; init; }
    public string? Reason { get; init; }
    public decimal? AreaInSqWa { get; init; }
    public string? Remark { get; init; }
}

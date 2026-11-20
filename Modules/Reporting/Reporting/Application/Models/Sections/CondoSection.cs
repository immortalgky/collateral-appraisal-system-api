namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "รายละเอียดห้องชุด" (Condo Details) section — FSD §2.1.2.3.
///
/// All scalar properties are nullable so that partial data renders as blank.
/// The section is absent from the report when <see cref="Providers.Sections.CondoSectionLoader.LoadAsync"/>
/// returns <see langword="null"/> (appraisal has no condo properties).
/// </summary>
public sealed class CondoSection
{
    // ── Unit rows (รายการห้องชุด) ──────────────────────────────────────────────────

    /// <summary>
    /// Per-unit area breakdown rows.
    /// One row per <c>appraisal.CondoAppraisalDetails</c> record linked to the appraisal,
    /// with areas derived from <c>appraisal.CondoAppraisalAreaDetails</c>.
    /// </summary>
    public IReadOnlyList<CondoUnitAreaRow> Units { get; init; } = [];

    // ── Building identification ───────────────────────────────────────────────────

    /// <summary>ชื่ออาคารชุด — source: CondoAppraisalDetails.CondoName.</summary>
    public string? CondoName { get; init; }

    /// <summary>อาคารเลขที่ — source: CondoAppraisalDetails.BuildingNumber.</summary>
    public string? BuildingNumber { get; init; }

    /// <summary>
    /// ทะเบียนอาคารชุดเลขที่ — source: CondoAppraisalDetails.CondoRegistrationNumber.
    /// </summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>
    /// โฉนดที่ดินเลขที่ (underlying land title) —
    /// source: CondoAppraisalDetails.BuiltOnTitleNumber.
    /// </summary>
    public string? BuiltOnTitleNumber { get; init; }

    // ── Address ────────────────────────────────────────────────────────────────────

    /// <summary>ตำบล / แขวง — source: CondoAppraisalDetails.SubDistrict (Address VO).</summary>
    public string? SubDistrict { get; init; }

    /// <summary>อำเภอ / เขต — source: CondoAppraisalDetails.District (Address VO).</summary>
    public string? District { get; init; }

    /// <summary>จังหวัด — source: CondoAppraisalDetails.Province (Address VO).</summary>
    public string? Province { get; init; }

    /// <summary>
    /// เนื้อที่ (FSD §2.1.2.3 field 15) — underlying LAND-plot area in ไร่-งาน-ตารางวา.
    /// No land-area column exists on CondoAppraisalDetails (only the unit's UsableArea,
    /// which is a different quantity), so this is rendered empty (null) until a proper
    /// source is available — the loader does NOT fall back to the unit's m².
    /// </summary>
    // no source for rai/ngan/wa land area on condo — render empty
    public string? LandAreaText { get; init; }

    /// <summary>ตั้งอยู่บนถนน — source: CondoAppraisalDetails.Street.</summary>
    public string? LocatedOnRoad { get; init; }

    /// <summary>ซอย — source: CondoAppraisalDetails.Soi.</summary>
    public string? Soi { get; init; }

    /// <summary>
    /// ระยะทาง (metres from main road) —
    /// source: CondoAppraisalDetails.DistanceFromMainRoad.
    /// Formatted as display string by loader.
    /// </summary>
    public string? DistanceText { get; init; }

    // ── Stored on CondoAppraisalDetails and printed by the numbered condo book ────────────

    /// <summary>สำนักงานที่ดิน — CondoAppraisalDetails.LandOffice (parameter group 'LandOffice').</summary>
    public string? LandOffice { get; init; }

    /// <summary>พื้นที่ใช้สอย (ตร.ม.) — sum of CondoAppraisalDetails.UsableArea over the condo units; the area GovernmentPrice is stored against.</summary>
    public decimal? UsableArea { get; init; }

    /// <summary>ตรวจสอบกรรมสิทธิ์แล้ว — CondoAppraisalDetails.IsOwnerVerified.</summary>
    public bool? IsOwnerVerified { get; init; }

    /// <summary>สภาพห้อง / สภาพอาคาร — CondoAppraisalDetails.BuildingConditionType (+ Other), group 'BuildingCondition'.</summary>
    public string? BuildingCondition { get; init; }

    /// <summary>จำนวนช่องจราจร (เลน) — CondoAppraisalDetails.NumberOfLanes.</summary>
    public short? NumberOfLanes { get; init; }

    /// <summary>เขตทางกว้าง (metres) — CondoAppraisalDetails.RightOfWayWidth.</summary>
    public decimal? RightOfWayWidth { get; init; }

    /// <summary>ปีที่สร้าง — CondoAppraisalDetails.ConstructionYear.</summary>
    public int? ConstructionYear { get; init; }

    /// <summary>พิกัด GPS — CondoAppraisalDetails.Latitude / Longitude.</summary>
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

    /// <summary>หมายเหตุแนวเวนคืน (ExpropriationLineRemark only), พรฎ. และหมายเหตุแนวเขตป่า.</summary>
    public string? ExpropriationRemark { get; init; }
    public string? RoyalDecree { get; init; }
    public string? ForestBoundaryRemark { get; init; }

    /// <summary>ข้อจำกัดอื่นๆ ที่พบ — CondoAppraisalDetails.OtherLegalLimitations (free text, up to 4000 chars).</summary>
    public string? OtherLegalLimitations { get; init; }

    /// <summary>ปัญหาน้ำท่วมขัง (book 1.5) — CondoAppraisalDetails.HasFloodProblem (+ FloodProblemRemark).</summary>
    public bool? HasFloodProblem { get; init; }
    public string? FloodProblemRemark { get; init; }

    /// <summary>ปัญหามลพิษ (book 1.5) — HasPollutionProblem; PollutionType is the labels joined " / " (code 99 prints PollutionTypeOther).</summary>
    public bool? HasPollutionProblem { get; init; }
    public string? PollutionType { get; init; }
    public string? PollutionProblemRemark { get; init; }

    /// <summary>ปัญหาอื่นๆ (book 1.5) — HasOtherEnvironmentProblem (+ OtherEnvironmentProblemRemark, printed as the row's value).</summary>
    public bool? HasOtherEnvironmentProblem { get; init; }
    public string? OtherEnvironmentProblemRemark { get; init; }

    /// <summary>ถนนผ่านหน้า (book 1.3) — CondoAppraisalDetails.RoadPassInFrontOfLand; the template falls back to <see cref="LocatedOnRoad"/> when blank.</summary>
    public string? RoadPassInFrontOfLand { get; init; }

    /// <summary>การคมนาคม (book 1.3) — TransportationAccessType decoded with the Transportation parameter names, plus the "other" text.</summary>
    public string? TransportationAccessType { get; init; }

    // ── Ownership &amp; obligation ──────────────────────────────────────────────────

    /// <summary>ผู้ถือกรรมสิทธิ์ — source: CondoAppraisalDetails.OwnerName.</summary>
    public string? OwnerName { get; init; }

    /// <summary>
    /// The owners of every unit, worded by the letter's rule (<see cref="Providers.LetterOwnerRows.Summarise"/>), with no "(รายละเอียดตามข้อ …)" pointer:
    /// all units alike → that one name; otherwise one "ห้อง {room}: {owner}" line per unit ("\n"-separated, the row is pre-line). A unit with no owner is left out;
    /// null when none has one (the template prints its own dash). <paramref name="grouped"/> counts per owner above 10 units (the condo summary); the book
    /// is the detail place and never groups.
    /// </summary>
    public string? Owners(bool grouped = false) => PerUnit(u => Providers.AppraisalSummaryCommonLoader.OwnerDisplay(u.OwnerName, u.IsOwnerVerified), null, grouped);

    /// <summary>The mortgage of every unit, per room like <see cref="Owners"/>, with the letter's "ไม่มี" rule: all alike → that one text; units with none are left out; all none → "ไม่มี".</summary>
    public string? Mortgage(bool grouped = false) => PerUnit(u => u.ObligationMortgage, Providers.ObligationText.None, grouped);

    /// <summary>The other obligations of every unit, the same rule as <see cref="Mortgage"/>.</summary>
    public string? Others(bool grouped = false) => PerUnit(u => u.ObligationOthers, Providers.ObligationText.None, grouped);

    /// <summary>The book's per-room texts (one line per room, never grouped): the ผู้ถือกรรมสิทธิ์ and ภาระผูกพัน rows of the condo 2.1 header and of the mixed book.</summary>
    public string? OwnersText => Owners();
    public string? ObligationMortgageText => Mortgage();
    public string? ObligationOthersText => Others();

    private string? PerUnit(Func<CondoUnitAreaRow, string?> text, string? noneText, bool grouped) =>
        Providers.LetterOwnerRows.Summarise(
            Units.Select((u, i) => (Formatting.ThaiAddressFormatter.RoomLabel(u.RoomNumber, i), text(u), 1)).ToList(),
            "ห้อง", null, noneText, grouped);

    /// <summary>ภาระผูกพันการจำนอง — source: ObligationType (02) + MortgageDetails; "ไม่มี" when not mortgaged.</summary>
    public string? ObligationMortgage { get; init; }

    /// <summary>ภาระผูกพันอื่นๆ — source: ObligationType (03/04/05/99) + ObligationDetails; "ไม่มี" when none.</summary>
    public string? ObligationOthers { get; init; }

    /// <summary>การให้เช่า — source: CondoAppraisalDetails.IsRentedOut.</summary>
    public bool? IsRentedOut { get; init; }

    // ── Position / collateral correctness ────────────────────────────────────────

    /// <summary>
    /// ตำแหน่งห้องชุด — ถูกต้อง / ไม่ถูกต้อง checkbox.
    /// Source: no dedicated boolean column on CondoAppraisalDetails; deferred (null).
    /// </summary>
    // no source — CondoAppraisalDetailConfiguration has no PositionCorrect column
    public bool? PositionCorrect { get; init; }

    /// <summary>ผลตรวจสอบเอกสาร — CondoAppraisalDetails.DocumentValidationResultType: "01" ถูกต้องตรงกัน, "02" ไม่สอดคล้องกัน; null otherwise.</summary>
    public string? DocumentValidationResult { get; init; }

    /// <summary>หมายเหตุตำแหน่งห้องชุด — CondoAppraisalDetails.LocationRemark; printed only when the position is ไม่ถูกต้อง.</summary>
    public string? PositionRemark { get; init; }

    // ── Interior finishes (สภาพและการตกแต่งภายในห้องชุด) ─────────────────────────

    /// <summary>
    /// วัสดุผิวพื้น — source: CondoAppraisalDetails.GroundFloorMaterialType
    /// (parameter-translated display string; Other suffix appended when present).
    /// </summary>
    public string? FloorMaterial { get; init; }

    /// <summary>วัสดุพื้นห้องน้ำ — source: CondoAppraisalDetails.BathroomFloorMaterialType.</summary>
    public string? BathroomFloorMaterial { get; init; }

    /// <summary>การตกแต่ง — source: CondoAppraisalDetails.DecorationType.</summary>
    public string? Decoration { get; init; }

    /// <summary>ผังห้อง — source: CondoAppraisalDetails.RoomLayoutType.</summary>
    public string? RoomLayout { get; init; }

    /// <summary>ทิวทัศน์ — source: CondoAppraisalDetails.LocationViewType (JSON array).</summary>
    public string? LocationView { get; init; }

    // Wall, ceiling, door, window and sanitary ware have no column on CondoAppraisalDetails;
    // the template prints a red "-" for them (they are not modelled here).

    // ── Road / access to building (ทางเข้าออกอาคารชุด) ──────────────────────────

    /// <summary>
    /// ผิวจราจรกว้าง (metres) — source: CondoAppraisalDetails.CarriagewayWidth.
    /// FSD field label: ผิวจราจรกว้าง.
    /// </summary>
    public decimal? CarriagewayWidth { get; init; }

    /// <summary>
    /// ลักษณะผิวจราจร — source: CondoAppraisalDetails.RoadSurfaceType
    /// (parameter-translated display string; Other suffix appended when present).
    /// </summary>
    public string? RoadSurfaceType { get; init; }

    /// <summary>
    /// สาธารณูปโภค (comma-joined display) —
    /// source: CondoAppraisalDetails.PublicUtilityType (JSON array decoded by loader).
    /// </summary>
    public string? PublicUtility { get; init; }

    // ── Building physical attributes ───────────────────────────────────────────────

    /// <summary>อายุอาคาร (ปี) — source: CondoAppraisalDetails.BuildingAge.</summary>
    public int? BuildingAge { get; init; }

    /// <summary>
    /// ความสูง (จำนวนชั้น) — source: CondoAppraisalDetails.NumberOfFloors.
    /// </summary>
    public decimal? BuildingHeightFloors { get; init; }

    /// <summary>
    /// รูปแบบ — source: CondoAppraisalDetails.BuildingFormType
    /// (parameter-translated display string; Other suffix appended when present).
    /// </summary>
    public string? BuildingForm { get; init; }

    /// <summary>
    /// วัสดุก่อสร้าง — source: CondoAppraisalDetails.ConstructionMaterialType
    /// (parameter-translated display string; Other suffix appended when present).
    /// </summary>
    public string? ConstructionMaterial { get; init; }

    /// <summary>
    /// หลังคา — source: CondoAppraisalDetails.RoofType
    /// (JSON array decoded + parameter-translated, comma-joined; Other suffix appended when present).
    /// </summary>
    public string? RoofType { get; init; }

    // ── Legal restrictions (ข้อกำหนดทางกฎหมาย) ──────────────────────────────────

    /// <summary>
    /// อยู่ในแนวเวนคืน checkbox —
    /// source: CondoAppraisalDetails.IsInExpropriationLine (bool?).
    /// </summary>
    public bool? IsInExpropriationLine { get; init; }

    /// <summary>
    /// ที่ดินประเภท — source: CondoAppraisalDetails.UrbanPlanningType (parameter-translated).
    /// </summary>
    public string? LandType { get; init; }

    /// <summary>เป็นพื้นที่สี / สีผังเมือง — CondoAppraisalDetails.UrbanPlanningColor (group 'UrbanPlanningColor'; 99 prints the Other text).</summary>
    public string? UrbanPlanningColor { get; init; }

    /// <summary>อยู่ในแนวเขตป่า — source: CondoAppraisalDetails.IsForestBoundary.</summary>
    public bool? IsForestBoundary { get; init; }

    /// <summary>สิ่งอำนวยความสะดวก — source: CondoAppraisalDetails.FacilityType (JSON array).</summary>
    public string? Facilities { get; init; }

    /// <summary>
    /// PropertyGroups.GroupNumber → the condo figures of the units that group holds. This section sums up EVERY unit of the appraisal
    /// (<see cref="UsableArea"/>, <see cref="CondoName"/> ... are the whole appraisal's), so a pricing grid's subject column reads its own
    /// group's figures from here.
    /// </summary>
    public IReadOnlyDictionary<int, CondoGroupFigures> GroupFigures { get; init; } = new Dictionary<int, CondoGroupFigures>();

    /// <summary>สภาพแวดล้อม — source: CondoAppraisalDetails.EnvironmentType (JSON array).</summary>
    public string? Environment { get; init; }

    // ── Legal restrictions, continued ────────────────────────────────────────────

    /// <summary>
    /// สิทธิทางเข้า-ออก — source: CondoAppraisalDetails.LandEntranceExitType
    /// (JSON-array-of-codes column, parameter-translated, comma-joined; code 99 shows the
    /// paired *Other free text instead of the generic "อื่นๆ" description).
    /// </summary>
    public string? EntryExitRights { get; init; }

    /// <summary>
    /// สภาพการใช้ประโยชน์ — source: CondoAppraisalDetails.LandUseType
    /// (JSON-array-of-codes column, parameter-translated, comma-joined; code 99 shows the
    /// paired *Other free text instead of the generic "อื่นๆ" description).
    /// </summary>
    public string? Utilization { get; init; }

    // ── Remark ────────────────────────────────────────────────────────────────────

    /// <summary>หมายเหตุ — source: CondoAppraisalDetails.Remark.</summary>
    public string? Remark { get; init; }

    // ── Valuation detail (รายละเอียดการประเมินมูลค่าทรัพย์สิน ห้องชุด) ──────────────

    /// <summary>
    /// Condo valuation breakdown table (FSD §2.1.2.x — Image #4).
    /// Null when the appraisal has no condo properties (same lifetime as the section).
    /// </summary>
    public CondoValuationDetail? Valuation { get; init; }
}

/// <summary>
/// Condo valuation table — FSD "รายละเอียดการประเมินมูลค่าทรัพย์สิน ห้องชุด" (Image #4).
///
/// PART A — ราคาประเมินทุนทรัพย์ห้องชุดในการจดทะเบียนสิทธิ์และนิติกรรม (government registration value).
///   Area components are sourced from CondoAppraisalAreaDetails; the per-sqm rate and amount come
///   from CondoAppraisalDetails.GovernmentPricePerSqm / GovernmentPrice (condo is priced per square
///   metre, unlike land which is priced per square wa).
///
/// PART B — ราคาประเมินโดยวิธีเปรียบเทียบราคาตลาด (market comparison value), sourced from the selected
///   PricingAnalysis → Approach → Method → PricingFinalValue.
///   Business rule: when the method is priced per-unit, the per-sqm cell shows "-" (the column is
///   NOT hidden) and the amount is the lump-sum value; otherwise the per-sqm rate is shown and
///   amount = rate × area. Empty numeric cells render as "-" throughout this table.
/// </summary>
public sealed class CondoValuationDetail
{
    // ── PART A: registration value ────────────────────────────────────────────────

    /// <summary>พื้นที่ภายในห้องชุด (ตร.ม.) — sum of all units' interior area.</summary>
    public decimal? InteriorArea { get; init; }

    /// <summary>พื้นที่ระเบียง (ตร.ม.) — sum of all units' balcony area.</summary>
    public decimal? BalconyArea { get; init; }

    /// <summary>พื้นที่วางแอร์ (ตร.ม.) — sum of all units' air-con-ledge area.</summary>
    public decimal? AirConArea { get; init; }

    /// <summary>พื้นที่อื่นๆ (ตร.ม.) — sum of all units' other area.</summary>
    public decimal? OtherArea { get; init; }

    /// <summary>
    /// ตร.ม. ละ (บาท) — government registration rate. Source: CondoAppraisalDetails.GovernmentPricePerSqm.
    /// </summary>
    public decimal? RegistrationPricePerSqm { get; init; }

    /// <summary>
    /// จำนวนเงิน (บาท) — government registration total. Source: CondoAppraisalDetails.GovernmentPrice
    /// (summed over the listed units).
    /// </summary>
    public decimal? RegistrationAmount { get; init; }

    /// <summary>Registration amount per area row = rate x that area (computed; null without a rate or area).</summary>
    /// <summary>หมายเหตุ of the valuation box — PricingAnalysis.Remark of the unit's group(s).</summary>
    public string? Remark { get; init; }

    public decimal? InteriorAmount { get; init; }
    public decimal? BalconyAmount { get; init; }
    public decimal? AirConAmount { get; init; }
    public decimal? OtherAmount { get; init; }

    // ── PART B: market comparison value ────────────────────────────────────────────

    /// <summary>พื้นที่ห้องชุดรวมระเบียง (ตร.ม.) — interior + balcony total.</summary>
    public decimal? MarketArea { get; init; }

    /// <summary>
    /// ตร.ม. ละ (บาท) — market price per sqm. Null (renders as "-") when the method is priced
    /// as a whole unit / lumpsum (UnitType == "PerUnit"). Otherwise sourced from
    /// PricingAnalysisMethod.ValuePerUnit, or derived as value ÷ area when not stored.
    /// </summary>
    public decimal? MarketPricePerSqm { get; init; }

    /// <summary>จำนวนเงิน (บาท) — market comparison amount (= rate × area, or lump sum when by-unit).</summary>
    public decimal? MarketAmount { get; init; }

    /// <summary>รวมราคาประเมินมูลค่าทรัพย์สิน (ปัดเศษ) — rounded grand total.</summary>
    public decimal? TotalRoundedValue { get; init; }
}

/// <summary>
/// Per-unit area row in the รายการห้องชุด table — FSD §2.1.2.3.
///
/// Areas are pivoted from <c>appraisal.CondoAppraisalAreaDetails</c> by matching
/// <c>AreaDescription</c> keywords:
///   "ภายใน" / "Interior"  → <see cref="InteriorArea"/>
///   "ระเบียง" / "Balcony"  → <see cref="BalconyArea"/>
///   "แอร์" / "AirCond"    → <see cref="AirConArea"/>
///   any other description  → <see cref="OtherArea"/> (accumulated)
/// <see cref="TotalArea"/> = sum of all four.
/// </summary>
public sealed class CondoUnitAreaRow
{
    /// <summary>1-based row sequence within the section.</summary>
    public int Sequence { get; init; }

    /// <summary>ห้องชุดเลขที่ — source: CondoAppraisalDetails.RoomNumber.</summary>
    public string? RoomNumber { get; init; }

    /// <summary>ชั้นที่ — source: CondoAppraisalDetails.FloorNumber.</summary>
    public string? FloorNumber { get; init; }

    /// <summary>ผู้ถือกรรมสิทธิ์ของห้องนี้ — source: CondoAppraisalDetails.OwnerName of THIS unit.</summary>
    public string? OwnerName { get; init; }

    /// <summary>CondoAppraisalDetails.IsOwnerVerified of this unit: only an explicit false means the owner could not be verified.</summary>
    public bool? IsOwnerVerified { get; init; }

    /// <summary>
    /// This unit's owner as the book prints it: "ไม่สามารถตรวจสอบได้" when marked not verified, otherwise the trimmed name, and "-" when there is none
    /// (the same rule as <see cref="BuildingSection.OwnerText"/>).
    /// </summary>
    public string OwnerText => Formatting.ThaiAddressFormatter.Stated(Providers.AppraisalSummaryCommonLoader.OwnerDisplay(OwnerName, IsOwnerVerified)) ?? "-";

    /// <summary>ภาระผูกพันการจำนองของห้องนี้ — "ไม่มี" when not mortgaged.</summary>
    public string? ObligationMortgage { get; init; }

    /// <summary>ภาระผูกพันอื่นๆ ของห้องนี้ — "ไม่มี" when none.</summary>
    public string? ObligationOthers { get; init; }

    /// <summary>
    /// พื้นที่ภายในห้องชุด (ตร.ม.) — pivoted from CondoAppraisalAreaDetails rows
    /// where AreaDescription contains "ภายใน" or "Interior" (case-insensitive).
    /// </summary>
    public decimal? InteriorArea { get; init; }

    /// <summary>
    /// พื้นที่ระเบียง (ตร.ม.) — pivoted from CondoAppraisalAreaDetails rows
    /// where AreaDescription contains "ระเบียง" or "Balcony" (case-insensitive).
    /// </summary>
    public decimal? BalconyArea { get; init; }

    /// <summary>
    /// พื้นที่วางแอร์ (ตร.ม.) — pivoted from CondoAppraisalAreaDetails rows
    /// where AreaDescription contains "แอร์" or "AirCond" (case-insensitive).
    /// </summary>
    public decimal? AirConArea { get; init; }

    /// <summary>
    /// พื้นที่อื่นๆ (ตร.ม.) — accumulated from all CondoAppraisalAreaDetails rows
    /// that do not match Interior / Balcony / AirCon keywords.
    /// </summary>
    public decimal? OtherArea { get; init; }

    /// <summary>
    /// พื้นที่รวม (ตร.ม.) — sum of InteriorArea + BalconyArea + AirConArea + OtherArea.
    /// Falls back to CondoAppraisalDetails.UsableArea when no area-detail rows exist.
    /// </summary>
    public decimal? TotalArea { get; init; }
}

/// <summary>The condo subject figures of ONE property group: the sum of its units' usable area, and the name / condition / facilities of its first unit (by SequenceInGroup).</summary>
public sealed class CondoGroupFigures
{
    public decimal? UsableArea { get; init; }
    public string? CondoName { get; init; }
    public string? BuildingCondition { get; init; }
    public string? Facilities { get; init; }
}

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
    /// เนื้อที่ — no standalone land-area column on CondoAppraisalDetails.
    /// Derived from sum of CondoAppraisalAreaDetails.AreaSize where applicable,
    /// expressed as total usable area in square metres (formatted string).
    /// </summary>
    // no source for rai/ngan/wa land area on condo — display total usable m² instead
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

    // ── Ownership &amp; obligation ──────────────────────────────────────────────────

    /// <summary>ผู้ถือกรรมสิทธิ์ — source: CondoAppraisalDetails.OwnerName.</summary>
    public string? OwnerName { get; init; }

    /// <summary>ภาระผูกพัน — source: CondoAppraisalDetails.ObligationDetails.</summary>
    public string? Obligation { get; init; }

    // ── Position / collateral correctness ────────────────────────────────────────

    /// <summary>
    /// ตำแหน่งหลักประกัน — ถูกต้อง / ไม่ถูกต้อง checkbox.
    /// Source: no dedicated boolean column on CondoAppraisalDetails; deferred (null).
    /// </summary>
    // no source — CondoAppraisalDetailConfiguration has no PositionCorrect column
    public bool? PositionCorrect { get; init; }

    // ── Interior finishes (สภาพและการตกแต่งภายในห้องชุด) ─────────────────────────

    /// <summary>
    /// วัสดุผิวพื้น — source: CondoAppraisalDetails.GroundFloorMaterialType
    /// (parameter-translated display string; Other suffix appended when present).
    /// </summary>
    public string? FloorMaterial { get; init; }

    /// <summary>
    /// ผนัง — no dedicated WallMaterialType column on CondoAppraisalDetails; deferred (null).
    /// The schema stores GroundFloorMaterialType / UpperFloorMaterialType / BathroomFloorMaterialType
    /// but no InteriorWallType on this entity.
    /// </summary>
    // no source — CondoAppraisalDetailConfiguration has no WallMaterialType column
    public string? Wall { get; init; }

    /// <summary>
    /// ฝ้าเพดาน — no dedicated CeilingMaterialType column on CondoAppraisalDetails; deferred (null).
    /// </summary>
    // no source — CondoAppraisalDetailConfiguration has no CeilingMaterialType column
    public string? Ceiling { get; init; }

    /// <summary>
    /// ประตู — no dedicated DoorMaterialType column on CondoAppraisalDetails; deferred (null).
    /// </summary>
    // no source — CondoAppraisalDetailConfiguration has no DoorMaterialType column
    public string? Door { get; init; }

    /// <summary>
    /// หน้าต่าง — no dedicated WindowMaterialType column on CondoAppraisalDetails; deferred (null).
    /// </summary>
    // no source — CondoAppraisalDetailConfiguration has no WindowMaterialType column
    public string? Window { get; init; }

    /// <summary>
    /// สุขภัณฑ์ — closest available column is BathroomFloorMaterialType (bathroom floor).
    /// Source: CondoAppraisalDetails.BathroomFloorMaterialType (parameter-translated display string).
    /// </summary>
    public string? Sanitary { get; init; }

    // ── Road / access to building (ทางเข้าออกอาคารชุด) ──────────────────────────

    /// <summary>
    /// ผิวจราจรกว้าง (metres) — source: CondoAppraisalDetails.AccessRoadWidth.
    /// FSD field label: ผิวจราจรกว้าง.
    /// </summary>
    public decimal? RoadSurfaceWidth { get; init; }

    /// <summary>
    /// เขตทางกว้าง (metres) — no RoadFrontage/RoadZoneWidth column on CondoAppraisalDetails; deferred (null).
    /// CondoAppraisalDetailConfiguration has AccessRoadWidth only (not a separate RoadZone/Frontage column).
    /// </summary>
    // no source — CondoAppraisalDetailConfiguration has no RoadZoneWidth/RoadFrontage column
    public decimal? RoadZoneWidth { get; init; }

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
    /// พื้นที่สี (urban-planning colour zone) —
    /// no UrbanPlanningType column on CondoAppraisalDetails; deferred (null).
    /// UrbanPlanningType exists only on LandAppraisalDetails and ProjectLand.
    /// </summary>
    // no source — CondoAppraisalDetailConfiguration has no UrbanPlanningType column
    public string? AreaColour { get; init; }

    /// <summary>
    /// ที่ดินประเภท —
    /// no LandZoneType / LandType column on CondoAppraisalDetails; deferred (null).
    /// </summary>
    // no source — CondoAppraisalDetailConfiguration has no LandZoneType / LandType column
    public string? LandType { get; init; }

    // ── Remark ────────────────────────────────────────────────────────────────────

    /// <summary>หมายเหตุ — source: CondoAppraisalDetails.Remark.</summary>
    public string? Remark { get; init; }
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

namespace Reporting.Application.Models.Sections;

/// <summary>
/// ViewModel for FSD §2.1.2.5 "รายละเอียดอาคาร" (Building Details section).
///
/// Covers three visual blocks on the printed form:
///   1. รายละเอียดสิ่งปลูกสร้าง — key/value header attributes.
///   2. โครงสร้างของอาคาร — structural component attributes.
///   3. รายการประเมินทรัพย์สินโดยวิธีต้นทุน — cost/depreciation table with totals.
///
/// All properties are nullable; absent data renders as a blank line rather than throwing.
/// Structure fields that store multi-select codes are flattened to a single display string
/// (comma-joined Thai labels) by the loader.
/// </summary>
public sealed class BuildingSection
{
    // ── Grouping (appraisal-book group-major layout; not displayed by the partial) ──

    /// <summary>กลุ่มที่ — PropertyGroups.GroupNumber for this property (0 = ungrouped).</summary>
    public int GroupNumber { get; init; }

    /// <summary>Group label — PropertyGroups.GroupName (null when ungrouped).</summary>
    public string? GroupName { get; init; }

    /// <summary>Building photo as a file:// URI (thumbnail, else first by sequence); null prints nothing.</summary>
    public string? PhotoSrc { get; init; }

    /// <summary>Every depreciation row is ส่วนพัฒนา (fence, paving, tank …): not a สิ่งปลูกสร้าง, printed only in the ส่วนพัฒนา section.</summary>
    public bool IsDevelopmentOnly { get; init; }

    /// <summary>สิ่งปลูกสร้างรายการที่ N — numbered across the whole book over real, appraised buildings only (0 for development-only and for IsAppraisable == false).</summary>
    public int BuildingNumber { get; set; }

    /// <summary>The non-building (IsBuilding = 0) depreciation rows — listed under ส่วนพัฒนาอื่นๆ บนที่ดิน.</summary>
    public IReadOnlyList<DevelopmentItem> DevelopmentItems { get; init; } = [];

    // ── Block 1: รายละเอียดสิ่งปลูกสร้าง ────────────────────────────────────────

    /// <summary>เลขที่บ้าน — Source: BuildingAppraisalDetails.HouseNumber (nvarchar 50).</summary>
    public string? HouseNumber { get; init; }

    /// <summary>BuildingAppraisalDetails.IsAppraisable: only an explicit false means the appraiser chose not to value this building; null / true are appraised.</summary>
    public bool? IsAppraisable { get; init; }

    /// <summary>เหตุผลที่ไม่ประเมินมูลค่า — BuildingAppraisalDetails.NotAppraisableReason, used when <see cref="IsAppraisable"/> is false.</summary>
    public string? NotAppraisableReason { get; init; }

    /// <summary>
    /// ประเภทอาคาร — Source: BuildingAppraisalDetails.BuildingType (code, nvarchar 100),
    /// translated to Thai via parameter.Parameters group='BuildingType' lang='TH'.
    /// Falls back to BuildingTypeOther then raw code.
    /// </summary>
    public string? BuildingTypeText { get; init; }

    /// <summary>จำนวนชั้น — Source: BuildingAppraisalDetails.NumberOfFloors (decimal 5,2).</summary>
    public decimal? NumberOfFloors { get; init; }

    /// <summary>รุ่น / แบบ — Source: BuildingAppraisalDetails.ModelName (nvarchar 100).</summary>
    public string? ModelName { get; init; }

    /// <summary>ชื่ออาคาร — Source: BuildingAppraisalDetails.PropertyName (nvarchar 200).</summary>
    public string? BuildingName { get; init; }

    /// <summary>ชื่อเจ้าของ — Source: BuildingAppraisalDetails.OwnerName (nvarchar 200).</summary>
    public string? OwnerName { get; init; }

    /// <summary>BuildingAppraisalDetails.IsOwnerVerified: only an explicit false means the owner could not be verified; null / true are as stated.</summary>
    public bool? IsOwnerVerified { get; init; }

    /// <summary>BuildingAppraisalDetails.IsUnderConstruction: only an explicit true means the building is still being built.</summary>
    public bool? IsUnderConstruction { get; init; }

    /// <summary>
    /// How far the construction has got, in percent: ConstructionInspections.SummaryCurrentProgressPct clamped to 0-100 (see BuildingSectionLoader).
    /// Null when there is no inspection or no figure was ever keyed; it is never filled in with a made-up 0.
    /// </summary>
    public decimal? ConstructionProgressPct { get; init; }

    /// <summary>
    /// The suffix of the item text in the 3.2 table: null unless the building is under construction, then "(กำลังก่อสร้าง 59.5%)" with the
    /// progress as stored (see <see cref="ProgressPctText"/>), or "(กำลังก่อสร้าง)" when no progress is known. A building flagged under
    /// construction whose progress already reads 100% is finished, as in the summary form, so it gets nothing.
    /// </summary>
    public string? UnderConstructionText => IsUnderConstruction != true || ConstructionProgressPct >= 100m
        ? null
        : ConstructionProgressPct is { } pct
            ? $"(กำลังก่อสร้าง {ProgressPctText(pct)}%)"
            : "(กำลังก่อสร้าง)";

    /// <summary>
    /// A progress figure as the book prints it: the figure as stored, up to 2 decimals with trailing zeros dropped (99.6 reads 99.6, 65 reads 65,
    /// 59.5 reads 59.5). 100 and above reads 100. Below 100 the extra decimals are cut, not rounded: the stored columns hold up to 4 decimals
    /// (ConstructionInspections.SummaryCurrentProgressPct is decimal(7,4), and the inspection total is a computed sum), so rounding could turn
    /// 99.996 into "100" on an unfinished building; truncating keeps it 99.99. Shared by the 3.2 item text and the cover's inspection line.
    /// </summary>
    public static string ProgressPctText(decimal pct) =>
        (pct >= 100m ? 100m : Math.Truncate(pct * 100m) / 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The owner as the book prints it, in ONE place (the 3.2 table and the building page both read it): "ไม่สามารถตรวจสอบได้" when the owner
    /// is marked not verified, otherwise the trimmed name, and "-" when there is none.
    /// </summary>
    public string OwnerText => IsOwnerVerified == false
        ? Providers.AppraisalSummaryCommonLoader.OwnerNotVerifiedText
        : string.IsNullOrWhiteSpace(OwnerName) ? "-" : OwnerName.Trim();

    /// <summary>
    /// เลขที่โฉนดที่ดินที่ตั้งอาคาร — Source: BuildingAppraisalDetails.BuiltOnTitleNumber (nvarchar 100).
    /// </summary>
    public string? BuiltOnTitleNumber { get; init; }

    /// <summary>
    /// แหล่งที่มาใบอนุญาตก่อสร้าง — no dedicated source column; deferred (null).
    /// // no source
    /// </summary>
    public string? LicenseSource { get; init; }

    /// <summary>เลขที่ใบอนุญาตก่อสร้าง — BuildingAppraisalDetails.ConstructionLicenseNumber.</summary>
    public string? LicenseNumber { get; init; }

    /// <summary>ใบอนุญาตก่อสร้างหมดอายุ — BuildingAppraisalDetails.ConstructionLicenseExpirationDate.</summary>
    public DateTime? LicenseExpiration { get; init; }

    /// <summary>มูลค่าประกันอัคคีภัย (ปัดเศษ) — BuildingAppraisalDetails.BuildingInsurancePrice.</summary>
    public decimal? InsurancePrice { get; init; }

    /// <summary>
    /// True when the cost table below is only a reference: the group's valuation did not use the Cost approach.
    /// Printed as "(อ้างอิง)" on its heading.
    /// </summary>
    public bool CostIsReference { get; init; }

    /// <summary>มูลค่าวิธีต้นทุนของกลุ่ม — PricingAnalysisApproaches.ApproachValue of the Cost approach (selected or not).</summary>
    public decimal? CostApproachValue { get; init; }

    /// <summary>ที่ดิน (วิธีต้นทุน) — the Cost approach's selected Land-role method value.</summary>
    public decimal? CostLandValue { get; init; }

    /// <summary>หมายเหตุและสมมติฐาน of the group's BuildingCost method (PricingAnalysisMethods.Remark); null when blank.</summary>
    public string? CostMethodRemark { get; init; }

    /// <summary>พื้นที่ใช้สอย (ตร.ม.) — Source: BuildingAppraisalDetails.TotalBuildingArea (decimal 18,4).</summary>
    public decimal? UsableArea { get; init; }

    /// <summary>
    /// ขนาด กว้าง × ยาว (เมตร) — Source: BuildingAppraisalDetails.BuildingWidth / BuildingLength, printed "W x L".
    /// </summary>
    public string? SizeText { get; init; }

    /// <summary>การแบ่งพื้นที่ใช้ประโยชน์ — Source: BuildingAppraisalDetails.SpaceDivision (free text).</summary>
    public string? SpaceDivision { get; init; }

    /// <summary>งานระบบในอาคาร — Source: BuildingAppraisalDetails.BuildingSystemType (JSON array, group 'BuildingSystem') + BuildingSystemTypeOther.</summary>
    public string? BuildingSystems { get; init; }

    /// <summary>อายุอาคาร (ปี) — Source: BuildingAppraisalDetails.BuildingAge (int).</summary>
    public int? BuildingAge { get; init; }

    /// <summary>
    /// สภาพอาคาร — Source: BuildingAppraisalDetails.BuildingConditionType (code, nvarchar 50),
    /// translated to Thai via parameter.Parameters group='BuildingCondition' lang='TH'.
    /// Falls back to BuildingConditionTypeOther then raw code.
    /// </summary>
    public string? BuildingCondition { get; init; }

    /// <summary>
    /// การดูแล บำรุงรักษา — Source: BuildingAppraisalDetails.MaintenanceType (code, parameter group
    /// 'BuildingMaintenance') + MaintenanceTypeOther; resolved to Thai by the loader.
    /// </summary>
    public string? Maintenance { get; init; }

    /// <summary>
    /// คุณภาพวัสดุ — Source: BuildingAppraisalDetails.BuildingMaterialType (nvarchar 100).
    /// Not a parameter-translated code; stored as a free-text / short label.
    /// </summary>
    public string? MaterialQuality { get; init; }

    /// <summary>
    /// รูปแบบอาคาร — Source: BuildingAppraisalDetails.BuildingStyleType (nvarchar 100).
    /// </summary>
    public string? BuildingForm { get; init; }

    /// <summary>
    /// การใช้ประโยชน์ — Source: BuildingAppraisalDetails.UtilizationType (nvarchar 100)
    /// with UtilizationTypeOther fallback.
    /// </summary>
    public string? Utilization { get; init; }

    // ── Block 2: โครงสร้างของอาคาร ──────────────────────────────────────────────

    /// <summary>
    /// โครงสร้างหลัก — Source: BuildingAppraisalDetails.StructureType (JSON array, nvarchar 500)
    /// + StructureTypeOther; resolved to Thai (parameter group 'GeneralStructure') by the loader.
    /// </summary>
    public string? MainStructure { get; init; }

    /// <summary>
    /// โครงหลังคา (FSD #17 Roof) — Source: BuildingAppraisalDetails.RoofFrameType (JSON array)
    /// + RoofFrameTypeOther; resolved to Thai (parameter group 'RoofFrame') by the loader.
    /// </summary>
    public string? RoofFrame { get; init; }

    /// <summary>
    /// วัสดุหลังคา (FSD #18 Roof surface) — Source: BuildingAppraisalDetails.RoofType (JSON array)
    /// + RoofTypeOther; resolved to Thai (parameter group 'Roof') by the loader.
    /// </summary>
    public string? RoofMaterial { get; init; }

    /// <summary>
    /// ผนัง — Source: BuildingAppraisalDetails.ExteriorWallType (JSON array) + InteriorWallType (JSON array),
    /// both joined; outer wall shown first, inner appended if distinct.
    /// </summary>
    public string? Wall { get; init; }

    /// <summary>
    /// ฝ้าเพดาน — Source: BuildingAppraisalDetails.CeilingType (JSON array, nvarchar 500)
    /// + CeilingTypeOther; joined by loader.
    /// </summary>
    public string? Ceiling { get; init; }

    /// <summary>
    /// การทาสี / ตกแต่ง — Source: BuildingAppraisalDetails.DecorationType (nvarchar 100)
    /// + DecorationTypeOther fallback.
    /// </summary>
    public string? Painting { get; init; }

    /// <summary>
    /// ประตู — Source: BuildingAppraisalDetails.DoorType (JSON array, group 'Door') + DoorTypeOther.
    /// </summary>
    public string? Door { get; init; }

    /// <summary>
    /// หน้าต่าง — Source: BuildingAppraisalDetails.WindowType (JSON array, group 'Window') + WindowTypeOther.
    /// </summary>
    public string? Window { get; init; }

    /// <summary>
    /// ห้องน้ำและสุขภัณฑ์ — Source: BuildingAppraisalDetails.SanitaryType (JSON array, group 'Sanitary') + SanitaryTypeOther.
    /// </summary>
    public string? Sanitary { get; init; }

    /// <summary>
    /// การตกแต่ง — Source: BuildingAppraisalDetails.ConstructionType (nvarchar 100)
    /// + ConstructionTypeOther fallback (เก็บรายละเอียดการตกแต่งภายใน).
    /// </summary>
    public string? Decoration { get; init; }

    /// <summary>
    /// รายละเอียดพื้น (FSD #22–26) — per-floor-range surface rows.
    /// Source: appraisal.BuildingAppraisalSurfaces (owned by BuildingAppraisalDetail).
    /// </summary>
    public IReadOnlyList<BuildingFloorRow> Floors { get; init; } = [];

    // ── Block 3: Cost / depreciation table ──────────────────────────────────────

    /// <summary>
    /// Rows in the "รายการประเมินทรัพย์สินโดยวิธีต้นทุน" cost table.
    /// Source: appraisal.BuildingDepreciationDetails (FK: BuildingAppraisalDetailId).
    /// </summary>
    public IReadOnlyList<BuildingCostRow> CostRows { get; init; } = [];

    /// <summary>รวมพื้นที่ (ตร.ม.) — SUM(BuildingDepreciationDetails.Area).</summary>
    public decimal? TotalArea { get; init; }

    /// <summary>รวมมูลค่าทดแทนใหม่ — SUM(BuildingDepreciationDetails.PriceBeforeDepreciation).</summary>
    public decimal? TotalPrice { get; init; }

    /// <summary>รวมมูลค่าหลังหักค่าเสื่อม — SUM(BuildingDepreciationDetails.PriceAfterDepreciation).</summary>
    public decimal? TotalValueAfterDepreciation { get; init; }

    /// <summary>The cost rows' own total (sum of the depreciated values, rounded to the nearest 1,000), whatever was keyed on the form.</summary>
    public decimal? CostRowsTotal { get; init; }

    /// <summary>The Building Cost Value the appraiser keyed, when it is not <see cref="CostRowsTotal"/>; null otherwise.</summary>
    public decimal? CostOverride { get; init; }

    /// <summary>หมายเหตุ — Source: BuildingAppraisalDetails.Remark (nvarchar 4000).</summary>
    public string? Remark { get; init; }
}

/// <summary>
/// One row in the cost / depreciation table (รายการประเมินทรัพย์สินโดยวิธีต้นทุน).
/// Source: appraisal.BuildingDepreciationDetails.
/// </summary>
public sealed class BuildingCostRow
{
    /// <summary>ลำดับ — 1-based sequence within the section.</summary>
    public int Sequence { get; init; }

    /// <summary>รายการสิ่งปลูกสร้าง — Source: BuildingDepreciationDetails.AreaDescription (nvarchar 200).</summary>
    public string? AreaItemName { get; init; }

    /// <summary>พื้นที่ (ตร.ม.) — Source: BuildingDepreciationDetails.Area (decimal 18,4).</summary>
    public decimal? Area { get; init; }

    /// <summary>มูลค่าทดแทนใหม่ หน่วยละ — Source: BuildingDepreciationDetails.PricePerSqMBeforeDepreciation (decimal 18,2).</summary>
    public decimal? PricePerSqm { get; init; }

    /// <summary>รวม — Source: BuildingDepreciationDetails.PriceBeforeDepreciation (decimal 18,2).</summary>
    public decimal? Price { get; init; }

    /// <summary>อายุ (ปี) — Source: BuildingDepreciationDetails.Year (short).</summary>
    public short? AgeYears { get; init; }

    /// <summary>ค่าเสื่อม ปีละ % — Source: BuildingDepreciationDetails.DepreciationYearPct (decimal 7,4).</summary>
    public decimal? DepreciationPctPerYear { get; init; }

    /// <summary>รวม % — Source: BuildingDepreciationDetails.TotalDepreciationPct (decimal 7,4).</summary>
    public decimal? TotalDepreciationPct { get; init; }

    /// <summary>มูลค่าค่าเสื่อม — Source: BuildingDepreciationDetails.PriceDepreciation (decimal 18,2).</summary>
    public decimal? DepreciationAmount { get; init; }

    /// <summary>มูลค่าหลังหักค่าเสื่อม — Source: BuildingDepreciationDetails.PriceAfterDepreciation (decimal 18,2).</summary>
    public decimal? ValueAfterDepreciation { get; init; }

    /// <summary>วิธีคิดค่าเสื่อม — BuildingDepreciationDetails.DepreciationMethod: "เหมารวม" (Gross) / "รายช่วง" (Period).</summary>
    public string? DepreciationMethod { get; init; }

    /// <summary>ราคาต่อ ตร.ม. หลังหักค่าเสื่อม — Source: BuildingDepreciationDetails.PricePerSqMAfterDepreciation (decimal 18,2).</summary>
    public decimal? PricePerSqmAfterDepreciation { get; init; }

    /// <summary>The depreciation rate by year range — BuildingDepreciationPeriods (Gross = one row 1..age).</summary>
    public IReadOnlyList<DepreciationPeriodRow> Periods { get; init; } = [];
}

/// <summary>One BuildingDepreciationPeriods row: years AtYear..ToYear at DepreciationPerYear % a year.</summary>
public sealed class DepreciationPeriodRow
{
    public int AtYear { get; init; }
    public int ToYear { get; init; }
    public decimal DepreciationPerYear { get; init; }
}

/// <summary>
/// One per-floor-range row (FSD #22–26) in the building surfaces table.
/// Source: appraisal.BuildingAppraisalSurfaces.
/// </summary>
public sealed class BuildingFloorRow
{
    /// <summary>ชั้นที่ (จาก) — Source: BuildingAppraisalSurfaces.FromFloorNumber.</summary>
    public int FromFloor { get; init; }

    /// <summary>ถึงชั้นที่ — Source: BuildingAppraisalSurfaces.ToFloorNumber.</summary>
    public int ToFloor { get; init; }

    /// <summary>ประเภทพื้น — FloorType resolved to Thai (parameter group 'FloorType').</summary>
    public string? FloorType { get; init; }

    /// <summary>โครงสร้างพื้น — FloorStructureType resolved to Thai (group 'FloorStructure').</summary>
    public string? FloorStructure { get; init; }

    /// <summary>วัสดุปูพื้น — FloorSurfaceType resolved to Thai (group 'FloorSurface').</summary>
    public string? FloorSurface { get; init; }
}

/// <summary>One ส่วนพัฒนา line. <see cref="Area"/> is in square metres, the unit of the cost table's พื้นที่ column.</summary>
public sealed class DevelopmentItem
{
    public string? AreaDescription { get; init; }

    /// <summary>The parent property's building type (its Other text when code 99), printed as the item title; the description is the fallback.</summary>
    public string? BuildingTypeText { get; init; }

    public decimal? Area { get; init; }
    public short? AgeYears { get; init; }
    public string? Remark { get; init; }

    /// <summary>The owning property's IsAppraisable: only an explicit false drops this item from the 3.2 table (it is noted instead).</summary>
    public bool? IsAppraisable { get; init; }

    /// <summary>The owning property's reason for not valuing it (null when none is stated).</summary>
    public string? NotAppraisableReason { get; init; }
}

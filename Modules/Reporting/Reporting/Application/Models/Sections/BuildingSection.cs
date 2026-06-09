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
    // ── Block 1: รายละเอียดสิ่งปลูกสร้าง ────────────────────────────────────────

    /// <summary>เลขที่บ้าน — Source: BuildingAppraisalDetails.HouseNumber (nvarchar 50).</summary>
    public string? HouseNumber { get; init; }

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

    /// <summary>
    /// เลขที่โฉนดที่ดินที่ตั้งอาคาร — Source: BuildingAppraisalDetails.BuiltOnTitleNumber (nvarchar 100).
    /// </summary>
    public string? BuiltOnTitleNumber { get; init; }

    /// <summary>
    /// แหล่งที่มาใบอนุญาตก่อสร้าง — no dedicated source column; deferred (null).
    /// // no source
    /// </summary>
    public string? LicenseSource { get; init; }

    /// <summary>
    /// เลขที่ใบอนุญาตก่อสร้าง — no dedicated source column; deferred (null).
    /// BuildingAppraisalDetailConfiguration has ConstructionLicenseExpirationDate (DateTime?) only.
    /// // no source
    /// </summary>
    public string? LicenseNumber { get; init; }

    /// <summary>พื้นที่ใช้สอย (ตร.ม.) — Source: BuildingAppraisalDetails.TotalBuildingArea (decimal 18,4).</summary>
    public decimal? UsableArea { get; init; }

    /// <summary>
    /// ขนาด กว้าง × ยาว — no dedicated width/length columns in the entity; deferred (null).
    /// // no source
    /// </summary>
    public string? SizeText { get; init; }

    /// <summary>อายุอาคาร (ปี) — Source: BuildingAppraisalDetails.BuildingAge (int).</summary>
    public int? BuildingAge { get; init; }

    /// <summary>
    /// สภาพอาคาร — Source: BuildingAppraisalDetails.BuildingConditionType (code, nvarchar 50),
    /// translated to Thai via parameter.Parameters group='BuildingCondition' lang='TH'.
    /// Falls back to BuildingConditionTypeOther then raw code.
    /// </summary>
    public string? BuildingCondition { get; init; }

    /// <summary>
    /// การบำรุงรักษา — no dedicated maintenance column in the entity; deferred (null).
    /// // no source
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
    /// + StructureTypeOther; joined to a comma-separated display string by the loader.
    /// </summary>
    public string? MainStructure { get; init; }

    /// <summary>
    /// วัสดุหลังคา — Source: BuildingAppraisalDetails.RoofType (JSON array, nvarchar 500)
    /// + RoofTypeOther; joined by loader. RoofFrameType is the frame; RoofType is the covering material.
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
    /// ประตู — no dedicated door column; deferred (null).
    /// // no source
    /// </summary>
    public string? Door { get; init; }

    /// <summary>
    /// หน้าต่าง — no dedicated window column; deferred (null).
    /// // no source
    /// </summary>
    public string? Window { get; init; }

    /// <summary>
    /// สุขาภิบาล — no dedicated sanitary column; deferred (null).
    /// // no source
    /// </summary>
    public string? Sanitary { get; init; }

    /// <summary>
    /// การตกแต่ง — Source: BuildingAppraisalDetails.ConstructionType (nvarchar 100)
    /// + ConstructionTypeOther fallback (เก็บรายละเอียดการตกแต่งภายใน).
    /// </summary>
    public string? Decoration { get; init; }

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
}

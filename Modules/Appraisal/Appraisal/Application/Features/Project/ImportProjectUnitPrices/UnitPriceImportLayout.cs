using System.Linq.Expressions;

namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

/// <summary>The unit columns the price list reads, projected from ProjectUnits (read side, no entity).</summary>
public sealed record UnitSnapshot(
    Guid Id,
    int SequenceNumber,
    string? RoomNumber,
    string? CondoRegistrationNumber,
    string? PlotNumber,
    string? HouseNumber,
    int? Floor,
    string? TowerName,
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice,
    decimal? LandArea)
{
    /// <summary>The one definition of the mapping: the EF query translates it, <see cref="From"/> runs it in memory.</summary>
    public static readonly Expression<Func<ProjectUnit, UnitSnapshot>> Projection = u => new UnitSnapshot(
        u.Id, u.SequenceNumber, u.RoomNumber, u.CondoRegistrationNumber, u.PlotNumber, u.HouseNumber,
        u.Floor, u.TowerName, u.ModelType, u.UsableArea, u.SellingPrice, u.LandArea);

    private static readonly Func<ProjectUnit, UnitSnapshot> Compiled = Projection.Compile();

    public static UnitSnapshot From(ProjectUnit unit) => Compiled(unit);
}

/// <summary>The stored price columns the price list shows, projected from ProjectUnitPrices.</summary>
public sealed record PriceSnapshot(
    Guid ProjectUnitId, decimal? StandardPrice, decimal? AppraisalValue, decimal? ForceSellingPrice, decimal? CoverageAmount)
{
    /// <summary>The one definition of the mapping: the EF query translates it, <see cref="From"/> runs it in memory.</summary>
    public static readonly Expression<Func<ProjectUnitPrice, PriceSnapshot>> Projection = p => new PriceSnapshot(
        p.ProjectUnitId, p.StandardPrice, p.TotalAppraisalValueRounded, p.ForceSellingPrice, p.CoverageAmount);

    private static readonly Func<ProjectUnitPrice, PriceSnapshot> Compiled = Projection.Compile();

    public static PriceSnapshot From(ProjectUnitPrice price) => Compiled(price);
}

/// <summary>
/// A column that is compared with the unit in the system and reported when it differs, never written.
/// <see cref="Read"/> returns a string, int or decimal (or null).
/// </summary>
public sealed record CompareColumn(string Header, Func<UnitSnapshot, object?> Read, bool Numeric);

/// <summary>
/// Excel header set of the price list for one project type. Used by the parser, the matcher and the
/// template builder, so a downloaded template always parses back. Headers are the bank workbook's own
/// (Thai) text; matching is case- and whitespace-insensitive (see ProjectUnitExcelParser.NormalizeHeader).
/// </summary>
public sealed record UnitPriceImportLayout(
    string ProjectTypeName,
    string Key1Header,
    Func<UnitSnapshot, string?> Key1,
    string Key2Header,
    Func<UnitSnapshot, string?> Key2,
    IReadOnlyList<CompareColumn> CompareColumns,
    string StandardPriceHeader,
    string AppraisalValueHeader,
    string ForceSellingPriceHeader,
    string? CoverageHeader,   // null when the type has no fire-insurance column (Land)
    string? TowerHeader = null,
    Func<UnitSnapshot, string?>? Tower = null)
{
    public static readonly UnitPriceImportLayout Condo = new(
        "Condo",
        "ห้องชุด", u => u.RoomNumber,
        "ห้องชุดเลขที่", u => u.CondoRegistrationNumber,
        [
            new("ชั้นที่", u => u.Floor, true),
            new("อาคาร", u => u.TowerName, false),
            new("แบบห้องชุด", u => u.ModelType, false),
            new("พื้นที่ใช้สอย", u => u.UsableArea, true),
            new("ราคาขายโครงการ", u => u.SellingPrice, true)
        ],
        "ราคา (บาท/ตร.ม.)",
        "ราคาประเมินที่เสนอขออนุมัติ",
        "ราคาบังคับขาย (บาท)",
        "มูลค่าประกันอัคคีภัย",
        TowerHeader: "อาคาร",
        Tower: u => u.TowerName);   // Condo only: the same room number can exist in every tower

    public static readonly UnitPriceImportLayout LandAndBuilding = new(
        "LandAndBuilding",
        "เลขแปลง", u => u.PlotNumber,
        "เลขที่บ้าน", u => u.HouseNumber,
        [
            new("แบบบ้าน", u => u.ModelType, false),
            new("เนื้อที่ดิน (ตร.วา)", u => u.LandArea, true),
            new("พื้นที่ใช้สอย (ตร.ม.)", u => u.UsableArea, true),
            new("ราคาขาย", u => u.SellingPrice, true)
        ],
        "ราคามาตรฐาน (บาท)",
        "มูลค่าประเมิน",
        "ราคาบังคับขาย (บาท)",
        "มูลค่าประกันอัคคีภัย");

    /// <summary>A bare plot has no building to insure, so no fire-insurance column.</summary>
    public static readonly UnitPriceImportLayout Land = new(
        "Land",
        "เลขแปลง", u => u.PlotNumber,
        "เลขที่บ้าน", u => u.HouseNumber,
        [
            new("เนื้อที่ดิน (ตร.วา)", u => u.LandArea, true),
            new("ราคาขาย", u => u.SellingPrice, true)
        ],
        "ราคามาตรฐาน (บาท)",
        "มูลค่าประเมิน",
        "ราคาบังคับขาย (บาท)",
        null);

    /// <summary>The unit's keys as the matcher sees them: trimmed, and a blank or dash-only value is no key.</summary>
    public string? Key1Of(UnitSnapshot u) => UnitPriceExcelParser.CleanKey(Key1(u));

    public string? Key2Of(UnitSnapshot u) => UnitPriceExcelParser.CleanKey(Key2(u));

    public static UnitPriceImportLayout For(ProjectType projectType) =>
        projectType == ProjectType.Condo ? Condo
        : projectType.HasStructures() ? LandAndBuilding
        : Land;

    /// <summary>Every header, in template column order: keys, compare-only columns, prices.</summary>
    public IReadOnlyList<string> AllHeaders =>
    [
        Key1Header, Key2Header,
        .. CompareColumns.Select(c => c.Header),
        StandardPriceHeader, AppraisalValueHeader, ForceSellingPriceHeader,
        .. CoverageHeader is null ? [] : new[] { CoverageHeader }
    ];
}

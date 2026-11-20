using System.Globalization;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// The subject column of section 8: for one property group, the value of each comparison factor row, taken
/// from what the book already loaded (land / building / condo sections and the group row) — no extra SQL.
/// Keyed by MarketComparableFactors.FactorCode; a factor with no source here is absent and prints "-".
///
/// Factor → source
///   01 ประเภททรัพย์สิน          group row PropertyType (the label the book shows); when the summary has no row for the group, ที่ดิน / สิ่งปลูกสร้าง / ที่ดินพร้อมสิ่งปลูกสร้าง / ห้องชุด by what the group holds
///   02 เนื้อที่ดิน                group row TotalSquareWa (priced / net land area), else MarketLandArea, else the lands' NetAreaInWa; "… ตารางวา"
///   03 สภาพที่ดิน                first land LandFill
///   05 หน้ากว้างที่ดินติดถนน       first land RoadFrontage
///   09 ชื่อแบบ                   first real building ModelName
///   11 สภาพอาคาร                 first real building BuildingCondition (condo group: its first unit's BuildingCondition)
///   13 อายุอาคาร                 first real building BuildingAge
///   14 พื้นที่ใช้สอย              SUM of the real buildings' UsableArea (condo group: the sum of its units' UsableArea)
///   18 ชื่อโครงการ/หมู่บ้าน       condo group: its first unit's CondoName (land has no village in the model)
///   26 ผิวจราจร                  first land RoadSurfaceType
///   27 สิทธิการเข้าออก            first land LandEntranceExitType
///   29 การใช้ประโยชน์ปัจจุบัน      first land LandUseType
///   32 สิ่งอำนวยความสะดวก         condo group: its first unit's Facilities (a land / building property has no such field)
///   52 ทำเลแปลง                 first land PlotLocationType
///   54 รูปแบบอาคาร               first real building BuildingForm
///   28 and the price / date / source / remark / ใช้ในการวิเคราะห์ราคา rows: no source → "-"
/// "First" = the group's first land / first real building (development-only buildings are left out).
/// A condo-only group (every property is a condo unit, U / LSU; decided from the group's property types) has no land / building
/// section; it reads ITS OWN units' figures, CondoSection.GroupFigures[group]: the usable area summed over its units, and the name /
/// condition / facilities of its first unit in the group's own order (PropertyGroupItems.SequenceInGroup, the order the photo fallback
/// uses). The appraisal-wide CondoSection totals are never used, so several condo groups each get their own. A mixed, machine-only or
/// development-only group, or one with an untyped property, gets no condo figures.
/// </summary>
internal static class ComparisonSubject
{
    /// <summary>AppraisalProperties.PropertyType codes of a condo unit: U (condo), LSU (lease condo).</summary>
    private static readonly HashSet<string> CondoTypes = new(StringComparer.OrdinalIgnoreCase) { "U", "LSU" };

    /// <summary>
    /// One query: every group's property type codes. A property with no type is kept as an empty entry (it is NOT a condo unit, so it
    /// keeps its group from being condo-only); a group with no properties is simply absent.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> LoadGroupPropertyTypesAsync(
        System.Data.IDbConnection connection, Guid appraisalId, CancellationToken ct = default)
    {
        var rows = await Dapper.SqlMapper.QueryAsync<(int GroupNumber, string? PropertyType)>(connection, new Dapper.CommandDefinition(
            """
            SELECT pg.GroupNumber, ap.PropertyType
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PropertyGroupItems pgi ON pgi.PropertyGroupId = pg.Id
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            WHERE pg.AppraisalId = @appraisalId
            """, new { appraisalId }, cancellationToken: ct));
        return rows.GroupBy(r => r.GroupNumber)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.PropertyType?.Trim() ?? string.Empty).Distinct().ToList());
    }

    /// <summary>
    /// A group is condo-only when EVERY one of its properties is a condo unit (U / LSU), decided from the group's own property types as the
    /// Sale Grid does with ap.PropertyType. A mixed group, a machine-only or development-only one, and a group holding a property with no type
    /// are not.
    /// </summary>
    internal static bool IsCondoOnly(AppraisalSummaryModel model, int groupNumber) =>
        model.GroupPropertyTypes.TryGetValue(groupNumber, out var types) && types.Count > 0 && types.All(t => CondoTypes.Contains(t));

    public static IReadOnlyDictionary<string, string?> Values(AppraisalSummaryModel model, int groupNumber)
    {
        var detail = model.PropertyGroups.FirstOrDefault(g => g.GroupNumber == groupNumber);
        var row = model.Groups.Concat(model.ExtraSummaryForms.SelectMany(f => f.Groups))
            .FirstOrDefault(g => g.GroupNumber == groupNumber);

        // A condo-only group reads ITS OWN units' figures (the appraisal-wide CondoSection sums up every unit, so it cannot stand for one group).
        var condoOnly = IsCondoOnly(model, groupNumber);
        var condo = condoOnly ? model.CondoSection?.GroupFigures.GetValueOrDefault(groupNumber) : null;

        var landWa = row?.TotalSquareWa ?? row?.MarketLandArea;
        return Values(detail, row?.PropertyType, landWa, condo, condoOnly);
    }

    /// <summary>The mapping itself (pure, so it can be tested without a model).</summary>
    internal static IReadOnlyDictionary<string, string?> Values(
        PropertyGroupDetail? group, string? propertyTypeLabel, decimal? groupLandWa, CondoGroupFigures? condo, bool condoOnly = false)
    {
        var lands = group?.Lands ?? [];
        var buildings = group?.RealBuildings ?? [];
        var land = lands.FirstOrDefault();
        var building = buildings.FirstOrDefault();

        var landArea = groupLandWa ?? (lands.Sum(l => l.NetAreaInWa) is var net and > 0 ? net : (decimal?)null);
        var usable = buildings.Where(b => b.UsableArea.HasValue).Select(b => b.UsableArea!.Value).ToList();

        var v = new Dictionary<string, string?>
        {
            ["01"] = Text(propertyTypeLabel) ?? (lands.Count > 0 && buildings.Count > 0 ? "ที่ดินพร้อมสิ่งปลูกสร้าง" : lands.Count > 0 ? "ที่ดิน" : buildings.Count > 0 ? "สิ่งปลูกสร้าง" : condo is not null || condoOnly ? "ห้องชุด" : null),
            ["02"] = landArea.HasValue ? $"{Num(landArea.Value)} ตารางวา" : null,
            ["03"] = Text(land?.LandFill),
            ["05"] = land?.RoadFrontage is { } rf ? Num(rf) : null,
            ["09"] = Text(building?.ModelName),
            ["11"] = Text(building?.BuildingCondition),
            ["13"] = building?.BuildingAge?.ToString(CultureInfo.InvariantCulture),
            ["14"] = usable.Count > 0 ? Num(usable.Sum()) : null,
            ["26"] = Text(land?.RoadSurfaceType),
            ["27"] = Text(land?.LandEntranceExitType),
            ["29"] = Text(land?.LandUseType),
            ["52"] = Text(land?.PlotLocationType),
            ["54"] = Text(building?.BuildingForm),
        };

        // The subject's ภาพถ่าย: the first real building's photo; the loader falls back to the group's first property photo.
        if (buildings.Select(b => b.PhotoSrc).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) is { } photo)
            v[ComparisonSectionLoader.SubjectPhotoKey] = photo;

        if (condo is not null)
        {
            v["11"] = Text(condo.BuildingCondition);
            v["14"] = condo.UsableArea is { } ua ? Num(ua) : null;
            v["18"] = Text(condo.CondoName);
            v["32"] = Text(condo.Facilities);
        }

        return v;
    }

    private static string? Text(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Num(decimal d) => d.ToString("#,##0.##", CultureInfo.InvariantCulture);
}

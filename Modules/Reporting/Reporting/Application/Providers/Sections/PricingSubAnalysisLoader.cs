using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the market sub-analyses a pricing method owns, so the book prints them under that method.
///
/// Appraisal domain (PricingAnalysisSubjectType): a reference analysis is a PricingAnalysis row with
///   SubjectType 2 MachineryCostRef  AnchorId = the machine (AppraisalProperty)  → RCN of MachineryCost
///             3 IncomeLandRef     AnchorId = the income analysis                → land value of Income
///             4 LeaseholdLandRef  AnchorId = the leasehold analysis             → land value per sq.wa of Leasehold
///             5 RoomIncomeRef     AnchorRefKey = room-type code                 → room rate of Income
///             6 ProfitRentRef                                                   → market rent of ProfitRent
/// and HostMethodId = the PricingAnalysisMethod that owns the field (the cleanup scope). Each holds one Market
/// approach with the WQS / Sale Grid / Direct Comparison the appraiser ran. Reference methods are never
/// "selected", so a method prints when it carries a value or has comparables linked — an untouched one does not.
///
/// SubjectType 1 (ProjectModel) is the price of one block-project model: <see cref="LoadForModelsAsync"/>,
/// which picks the methods with the same rule as the property groups (<see cref="PricingMethodPicker.Pick"/>).
///
/// Each method renders with the existing WQS / Sale Grid loaders and partials (no subject column: a reference
/// analysis has no property group).
/// </summary>
internal static class PricingSubAnalysisLoader
{
    private static readonly string[] GridTypes = ["WQS", "SaleGrid", "DirectComparison"];

    /// <summary>The sub-heading printed above the analyses of one kind.</summary>
    internal static string Heading(int subjectType) => subjectType switch
    {
        1 => "ที่มาของราคาประเมิน แบบ",
        2 => "ที่มาของราคาทดแทนใหม่ (RCN)",
        3 => "ที่มาของมูลค่าที่ดิน",
        4 => "ที่มาของมูลค่าที่ดินต่อตารางวา",
        5 => "ที่มาของค่าห้องพัก",
        6 => "ที่มาของค่าเช่าตลาด",
        _ => "ที่มาของราคา"
    };

    internal static string MethodLabel(string? methodType) => methodType?.ToLowerInvariant() switch
    {
        "wqs" => "วิธีให้คะแนนคุณภาพถ่วงน้ำหนัก (WQS)",
        "salegrid" => "วิธีเปรียบเทียบตลาด (Sale Adjustment Grid)",
        "directcomparison" => "วิธีเปรียบเทียบตรง (Direct Comparison)",
        _ => methodType ?? ""
    };

    /// <summary>The sub-analyses hosted by one method, in print order.</summary>
    /// <remarks><paramref name="toApplicationTime"/> is required on every path here: a sub-analysis grid prints its Date factors with the same
    /// time-zone rule as the main grid, and an optional parameter would let a caller forget it.</remarks>
    public static async Task<IReadOnlyList<PricingSubAnalysis>> LoadForHostAsync(
        IDbConnection connection, Guid hostMethodId, Func<DateTime, DateTime> toApplicationTime, CancellationToken ct = default) =>
        (await LoadForHostsAsync(connection, [hostMethodId], toApplicationTime, ct)).Select(x => x.Sub).ToList();

    /// <summary>The sub-analyses hosted by any of <paramref name="hostMethodIds"/>, with the anchor each belongs to.</summary>
    public static async Task<IReadOnlyList<(Guid AnchorId, PricingSubAnalysis Sub)>> LoadForHostsAsync(
        IDbConnection connection, IReadOnlyCollection<Guid> hostMethodIds, Func<DateTime, DateTime> toApplicationTime, CancellationToken ct = default)
    {
        if (hostMethodIds.Count == 0) return [];

        var rows = (await connection.QueryAsync<RefRow>(new CommandDefinition(
            """
            SELECT pa.SubjectType, pa.AnchorId, pa.AnchorRefKey,
                   pam.Id AS PricingMethodId, pam.MethodType, pam.MethodValue, pam.ValuePerUnit, pam.UnitType, pam.Remark,
                   rt.RoomTypeName
            FROM appraisal.PricingAnalysis pa
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pam ON pam.ApproachId = paa.Id
            OUTER APPLY (SELECT TOP 1 p.[description] AS RoomTypeName
                         FROM parameter.Parameters p
                         WHERE pa.SubjectType = 5 AND p.[group] = 'RoomType' AND p.[language] = 'TH'
                           AND p.[isactive] = 1 AND p.[code] = pa.AnchorRefKey) rt
            WHERE pa.HostMethodId IN @HostIds
              AND pa.SubjectType BETWEEN 2 AND 6
              AND pam.MethodType IN @Types
              AND (pam.MethodValue IS NOT NULL
                   OR EXISTS (SELECT 1 FROM appraisal.PricingComparableLinks l WHERE l.PricingMethodId = pam.Id))
            -- the order the main grids print in: WQS before Sale Grid / Direct Comparison, then creation order (Guid order is arbitrary)
            ORDER BY pa.SubjectType, pa.AnchorRefKey, CASE WHEN pa.CreatedAt IS NULL THEN 1 ELSE 0 END, pa.CreatedAt, pa.Id,
                     paa.IsSelected DESC, CASE WHEN paa.CreatedAt IS NULL THEN 1 ELSE 0 END, paa.CreatedAt, paa.ApproachType, paa.Id,
                     CASE WHEN pam.MethodType = 'WQS' THEN 0 ELSE 1 END, CASE WHEN pam.CreatedAt IS NULL THEN 1 ELSE 0 END, pam.CreatedAt, pam.MethodType, pam.Id
            """,
            new { HostIds = hostMethodIds, Types = GridTypes }, cancellationToken: ct))).ToList();

        var result = new List<(Guid, PricingSubAnalysis)>(rows.Count);
        foreach (var r in rows)
        {
            var method = new PickedMethod
            {
                PricingMethodId = r.PricingMethodId,
                MethodType = r.MethodType,
                MethodValue = r.MethodValue,
                ValuePerUnit = r.ValuePerUnit,
                UnitType = r.UnitType
            };
            var caption = r.SubjectType == 5 && !string.IsNullOrWhiteSpace(r.AnchorRefKey)
                ? $"ประเภทห้อง {(string.IsNullOrWhiteSpace(r.RoomTypeName) ? r.AnchorRefKey.Trim() : r.RoomTypeName.Trim())}"
                : null;
            if (await LoadOneAsync(connection, method, Heading(r.SubjectType), caption, r.Remark, null, toApplicationTime, ct) is { } sub)
                result.Add((r.AnchorId, sub));
        }
        return result;
    }

    /// <summary>
    /// The analyses that priced each block-project model (PricingAnalysis SubjectType 1, AnchorId = ProjectModels.Id),
    /// keyed by model id. Only the methods that fed the model's value (no reference approaches).
    /// <paramref name="models"/>: per model id, its name (the heading) and standard usable area (a per-sqm final value).
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PricingSubAnalysis>>> LoadForModelsAsync(
        IDbConnection connection, IReadOnlyDictionary<Guid, (string? Name, decimal? UsableArea)> models, Func<DateTime, DateTime> toApplicationTime, CancellationToken ct = default)
    {
        if (models.Count == 0) return new Dictionary<Guid, IReadOnlyList<PricingSubAnalysis>>();

        var rows = (await connection.QueryAsync<PricingMethodPicker.Row>(new CommandDefinition(
            """
            SELECT pa.AnchorId AS PropertyGroupId, 0 AS GroupNumber, NULL AS GroupName,
                   paa.Id AS ApproachId, paa.ApproachType, paa.IsSelected AS ApproachSelected,
                   pam.Id AS PricingMethodId, pam.MethodType, pam.MethodValue, pam.ValuePerUnit, pam.UnitType,
                   pam.IsSelected AS MethodSelected, pam.Role, pam.Remark
            FROM appraisal.PricingAnalysis pa
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pam ON pam.ApproachId = paa.Id
            WHERE pa.SubjectType = 1 AND pa.AnchorId IN @ModelIds
            ORDER BY pa.AnchorId, paa.IsSelected DESC, CASE WHEN paa.CreatedAt IS NULL THEN 1 ELSE 0 END, paa.CreatedAt, paa.ApproachType, paa.Id, CASE WHEN pam.CreatedAt IS NULL THEN 1 ELSE 0 END, pam.CreatedAt, pam.MethodType, pam.Id
            """,
            new { ModelIds = models.Keys.ToList() }, cancellationToken: ct))).ToList();

        var byModel = new Dictionary<Guid, IReadOnlyList<PricingSubAnalysis>>();
        // PropertyGroupId carries the model id here (the anchor); Pick groups by approach, so models never mix.
        // WQS before Sale Grid / Direct Comparison, as the main grids print them (the sort is stable, so creation order is kept inside each kind).
        foreach (var m in PricingMethodPicker.Pick(rows, GridTypes).Where(m => !m.Context.IsReference)
                     .OrderBy(m => string.Equals(m.MethodType, "WQS", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
        {
            var model = models.GetValueOrDefault(m.PropertyGroupId);
            var heading = string.IsNullOrWhiteSpace(model.Name) ? Heading(1) : $"{Heading(1)} {model.Name.Trim()}";
            var sub = await LoadOneAsync(connection, m with { PropertyGroupId = Guid.Empty }, heading, null,
                m.Context.Remark, model.UsableArea, toApplicationTime, ct);
            if (sub is null) continue;
            var list = byModel.TryGetValue(m.PropertyGroupId, out var l) ? (List<PricingSubAnalysis>)l : [];
            list.Add(sub);
            byModel[m.PropertyGroupId] = list;
        }
        return byModel;
    }

    private static async Task<PricingSubAnalysis?> LoadOneAsync(
        IDbConnection connection, PickedMethod method, string heading, string? caption, string? remark,
        decimal? usableArea, Func<DateTime, DateTime> toApplicationTime, CancellationToken ct)
    {
        // The sub-analysis has no approach / role of its own: the context only carries the remark (the partial
        // prints it); book-sub-analyses hides the banner.
        var bare = method with { Context = new PricingMethodContext { Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim() } };
        var isWqs = string.Equals(method.MethodType, "WQS", StringComparison.OrdinalIgnoreCase);
        var wqs = isWqs ? await WqsSectionLoader.LoadOneAsync(connection, bare, ct, usableArea) : null;
        var grid = isWqs ? null : await SaleGridSectionLoader.LoadOneAsync(connection, bare, null, toApplicationTime, usableArea);
        if (wqs is null && grid is null) return null;
        return new PricingSubAnalysis
        {
            Heading = heading,
            Caption = caption,
            MethodLabel = MethodLabel(method.MethodType),
            Wqs = wqs,
            SaleGrid = grid
        };
    }

    private sealed class RefRow
    {
        public int SubjectType { get; init; }
        public Guid AnchorId { get; init; }
        public string? AnchorRefKey { get; init; }
        public Guid PricingMethodId { get; init; }
        public string MethodType { get; init; } = "";
        public decimal? MethodValue { get; init; }
        public decimal? ValuePerUnit { get; init; }
        public string? UnitType { get; init; }
        public string? Remark { get; init; }
        public string? RoomTypeName { get; init; }
    }
}

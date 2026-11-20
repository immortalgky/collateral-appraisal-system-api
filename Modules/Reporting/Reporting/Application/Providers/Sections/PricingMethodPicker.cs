using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// One pricing method the book prints, with the group it belongs to.
/// </summary>
internal sealed record PickedMethod
{
    public Guid PropertyGroupId { get; init; }
    public int GroupNumber { get; init; }
    public string? GroupName { get; init; }
    public Guid ApproachId { get; init; }
    public Guid PricingMethodId { get; init; }
    public string MethodType { get; init; } = "";
    public decimal? MethodValue { get; init; }
    public decimal? ValuePerUnit { get; init; }
    /// <summary>PerSqWa / PerSqm / PerUnit — PricingAnalysisMethods.UnitType (null on older rows).</summary>
    public string? UnitType { get; init; }
    /// <summary>The raw PricingAnalysisMethods.Role (a Cost approach holds one method per role).</summary>
    public string? Role { get; init; }
    /// <summary>The method's own IsSelected (false when it was picked only because it carries a value).</summary>
    public bool MethodSelected { get; init; }
    public PricingMethodContext Context { get; init; } = new();
}

/// <summary>
/// Single place that decides WHICH pricing methods the book prints, so the six pricing loaders cannot
/// drift apart (they used to take the first method by Id, which printed an unselected method).
///
/// Rule, applied per approach (PricingAnalysisApproaches row) across every method it holds:
///   1. The methods the appraiser selected (pam.IsSelected) — the same ones the domain rolls up into the
///      approach value.
///   2. When the approach has none selected, every method that carries a value.
///   3. An approach that is not the selected one (paa.IsSelected = 0) is only a reference: its methods
///      print, marked IsReference, but only when they carry a value (an untouched method prints nothing).
/// "Carries a value" means a non-zero MethodValue: the pricing screen calls a method with 0 "Pending", and a
/// section of nothing but zeros is not an analysis. A SELECTED method is never dropped by this (selecting one
/// needs a calculated value on the screen).
/// Methods that are not selected inside a selected approach (the alternatives the appraiser did not
/// choose) are not printed.
/// The rule runs over ALL method types of the approach and the requested types are filtered afterwards,
/// otherwise asking for "SaleGrid" alone would look like "nothing is selected here" and print it.
/// Output order: group number, selected approaches before reference ones, then method Id.
/// </summary>
internal static class PricingMethodPicker
{
    public static async Task<IReadOnlyList<PickedMethod>> LoadAsync(
        IDbConnection connection,
        Guid appraisalId,
        string[] methodTypes,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                pg.Id            AS PropertyGroupId,
                pg.GroupNumber,
                pg.GroupName,
                paa.Id           AS ApproachId,
                paa.ApproachType,
                paa.IsSelected   AS ApproachSelected,
                pam.Id           AS PricingMethodId,
                pam.MethodType,
                pam.MethodValue,
                pam.ValuePerUnit,
                pam.UnitType,
                pam.IsSelected   AS MethodSelected,
                pam.Role,
                pam.Remark
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PricingAnalysis pa
                ON pa.AnchorId = pg.Id
               AND pa.SubjectType = 0
            JOIN appraisal.PricingAnalysisApproaches paa
                ON paa.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pam
                ON pam.ApproachId = paa.Id
            WHERE pg.AppraisalId = @AppraisalId
            -- Guid order is arbitrary, so within an approach the methods come in the order they were created.
            ORDER BY pg.GroupNumber, paa.IsSelected DESC, CASE WHEN paa.CreatedAt IS NULL THEN 1 ELSE 0 END, paa.CreatedAt, paa.ApproachType, paa.Id, CASE WHEN pam.CreatedAt IS NULL THEN 1 ELSE 0 END, pam.CreatedAt, pam.MethodType, pam.Id
            """;

        var rows = (await connection.QueryAsync<Row>(
            new CommandDefinition(sql, new { AppraisalId = appraisalId }, cancellationToken: ct))).ToList();

        // The query already orders by group / selected-first / approach / method, and GroupBy keeps first-seen order.
        return Pick(rows, methodTypes);
    }

    /// <summary>The selection rule above, over rows already read (also used for the block-project model analyses).</summary>
    internal static IReadOnlyList<PickedMethod> Pick(IEnumerable<Row> rows, string[] methodTypes)
    {
        var picked = new List<PickedMethod>();
        foreach (var approach in rows.GroupBy(r => r.ApproachId))
        {
            var members = approach.ToList();
            var chosen = members.Where(m => m.MethodSelected).ToList();
            if (chosen.Count == 0)
                chosen = members.Where(m => HasValue(m.MethodValue)).ToList();

            foreach (var m in chosen)
            {
                if (!methodTypes.Contains(m.MethodType, StringComparer.OrdinalIgnoreCase))
                    continue;
                // Reference approach: an empty method is noise, not an analysis.
                if (!m.ApproachSelected && !HasValue(m.MethodValue))
                    continue;

                picked.Add(new PickedMethod
                {
                    PropertyGroupId = m.PropertyGroupId,
                    GroupNumber = m.GroupNumber,
                    GroupName = m.GroupName,
                    ApproachId = m.ApproachId,
                    PricingMethodId = m.PricingMethodId,
                    MethodType = m.MethodType,
                    MethodValue = m.MethodValue,
                    ValuePerUnit = m.ValuePerUnit,
                    UnitType = m.UnitType,
                    Role = m.Role,
                    MethodSelected = m.MethodSelected,
                    Context = new PricingMethodContext
                    {
                        ApproachType = m.ApproachType,
                        ApproachLabel = ApproachLabel(m.ApproachType),
                        RoleLabel = RoleLabel(m.Role),
                        IsReference = !m.ApproachSelected,
                        Remark = string.IsNullOrWhiteSpace(m.Remark) ? null : m.Remark
                    }
                });
            }
        }

        return picked;
    }

    internal static bool HasValue(decimal? methodValue) => methodValue is not null and not 0m;

    internal static string? ApproachLabel(string? approachType) => approachType?.ToLowerInvariant() switch
    {
        "market" => "วิธีเปรียบเทียบราคาตลาด",
        "cost" => "วิธีต้นทุน",
        "income" => "วิธีรายได้",
        "residual" => "วิธีสมมติฐาน (Residual)",
        _ => approachType
    };

    private static string? RoleLabel(string? role) => role?.ToLowerInvariant() switch
    {
        "land" => "ที่ดิน",
        "building" => "สิ่งปลูกสร้าง",
        "landandbuilding" => "ที่ดินพร้อมสิ่งปลูกสร้าง",
        "machinery" => "เครื่องจักร",
        _ => null
    };

    internal sealed class Row
    {
        public Guid PropertyGroupId { get; init; }
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public Guid ApproachId { get; init; }
        public string? ApproachType { get; init; }
        public bool ApproachSelected { get; init; }
        public Guid PricingMethodId { get; init; }
        public string MethodType { get; init; } = "";
        public decimal? MethodValue { get; init; }
        public decimal? ValuePerUnit { get; init; }
        public string? UnitType { get; init; }
        public bool MethodSelected { get; init; }
        public string? Role { get; init; }
        public string? Remark { get; init; }
    }
}

using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the lease agreement(s) behind a Leasehold / Profit Rent page: every LeaseAgreementDetails row of the
/// properties in the method's property group, each with that property's RentalInfos.
/// </summary>
internal static class LeaseAgreementLoader
{
    public static async Task<IReadOnlyList<LeaseAgreementBlock>> LoadAsync(
        IDbConnection connection, Guid propertyGroupId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                l.LessorName, l.LesseeName, l.ContractNo, l.LeaseStartDate, l.LeaseEndDate,
                l.LeasePeriodAsContract, l.RemainingLeaseAsAppraisalDate, l.LeaseRentFee, l.RentAdjust,
                l.Sublease, l.AdditionalExpenses, l.LeaseTerminate, l.ContractRenewal,
                l.RentalTermsImpactingPropertyUse, l.TerminationOfLease, l.Remark,
                l.IsLeaseRegistered, l.IsLeaseholdPledgeable,
                ri.NumberOfYears, ri.FirstYearStartDate, ri.ContractRentalFeePerYear, ri.UpFrontTotalAmount,
                ri.GrowthRateType, ri.GrowthRatePercent, ri.GrowthIntervalYears
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.LeaseAgreementDetails l ON l.AppraisalPropertyId = ap.Id
            LEFT JOIN appraisal.RentalInfos ri ON ri.AppraisalPropertyId = ap.Id
            WHERE pgi.PropertyGroupId = @GroupId
            ORDER BY pgi.SequenceInGroup, ap.SequenceNumber, l.Id
            """;

        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(sql, new { GroupId = propertyGroupId }, cancellationToken: ct));

        return rows
            // A lease form that was opened and never filled in has every column NULL — not an agreement.
            .Where(r => r.LessorName is not null || r.LesseeName is not null || r.ContractNo is not null
                        || r.LeaseStartDate is not null || r.LeaseEndDate is not null || r.LeaseRentFee is not null)
            .Select(r => new LeaseAgreementBlock
            {
                LessorName = r.LessorName,
                LesseeName = r.LesseeName,
                ContractNo = r.ContractNo,
                LeaseStartDate = r.LeaseStartDate,
                LeaseEndDate = r.LeaseEndDate,
                LeasePeriodAsContract = r.LeasePeriodAsContract,
                RemainingLeaseAsAppraisalDate = r.RemainingLeaseAsAppraisalDate,
                LeaseRentFee = r.LeaseRentFee,
                RentAdjust = r.RentAdjust,
                Sublease = r.Sublease,
                AdditionalExpenses = r.AdditionalExpenses,
                LeaseTerminate = r.LeaseTerminate,
                ContractRenewal = r.ContractRenewal,
                RentalTermsImpactingPropertyUse = r.RentalTermsImpactingPropertyUse,
                TerminationOfLease = r.TerminationOfLease,
                Remark = r.Remark,
                RegistrationText = YesNo(r.IsLeaseRegistered, "จดทะเบียน", "ไม่จดทะเบียน"),
                PledgeableText = YesNo(r.IsLeaseholdPledgeable, "ได้", "ไม่ได้"),
                NumberOfYears = r.NumberOfYears,
                FirstYearStartDate = r.FirstYearStartDate,
                ContractRentalFeePerYear = r.ContractRentalFeePerYear,
                UpFrontTotalAmount = r.UpFrontTotalAmount,
                GrowthText = GrowthText(r.GrowthRateType, r.GrowthRatePercent, r.GrowthIntervalYears)
            })
            .ToList();
    }

    /// <summary>
    /// The growth-rate mode as stored ("Frequency" = grow every N years, "Period" = by explicit periods) in Thai.
    /// Shared with the pages that print the raw mode (leasehold land growth).
    /// </summary>
    internal static string? GrowthTypeThai(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "frequency" => "ตามความถี่ (ทุกช่วงปี)",
        "period" => "ตามช่วงเวลา",
        null or "" => null,
        _ => type
    };

    /// <summary>Optional yes/no answer as printed; "-" when it was never answered.</summary>
    internal static string YesNo(bool? value, string yes, string no) => value is null ? "-" : value.Value ? yes : no;

    private static string? GrowthText(string? type, decimal? percent, int? intervalYears)
    {
        var parts = new List<string>();
        if (GrowthTypeThai(type) is { } t) parts.Add(t);
        if (percent.HasValue) parts.Add($"{percent.Value:N2}%");
        if (intervalYears is > 0) parts.Add($"ทุก {intervalYears} ปี");
        return parts.Count > 0 ? string.Join(" ", parts) : null;
    }

    private sealed class Row
    {
        public string? LessorName { get; init; }
        public string? LesseeName { get; init; }
        public string? ContractNo { get; init; }
        public DateTime? LeaseStartDate { get; init; }
        public DateTime? LeaseEndDate { get; init; }
        public decimal? LeasePeriodAsContract { get; init; }
        public decimal? RemainingLeaseAsAppraisalDate { get; init; }
        public decimal? LeaseRentFee { get; init; }
        public decimal? RentAdjust { get; init; }
        public string? Sublease { get; init; }
        public decimal? AdditionalExpenses { get; init; }
        public string? LeaseTerminate { get; init; }
        public string? ContractRenewal { get; init; }
        public string? RentalTermsImpactingPropertyUse { get; init; }
        public string? TerminationOfLease { get; init; }
        public string? Remark { get; init; }
        public bool? IsLeaseRegistered { get; init; }
        public bool? IsLeaseholdPledgeable { get; init; }
        public int? NumberOfYears { get; init; }
        public DateTime? FirstYearStartDate { get; init; }
        public decimal? ContractRentalFeePerYear { get; init; }
        public decimal? UpFrontTotalAmount { get; init; }
        public string? GrowthRateType { get; init; }
        public decimal? GrowthRatePercent { get; init; }
        public int? GrowthIntervalYears { get; init; }
    }
}

using Collateral.CollateralMasters.Models;
using Collateral.Contracts;
using Collateral.Contracts.FileInterface;
using Collateral.Data;
using Microsoft.EntityFrameworkCore;

namespace Collateral.CollateralMasters.CollateralResult;

public class CollateralResultQuery(CollateralDbContext db) : ICollateralResultQuery
{
    public async Task<IReadOnlyList<CollateralResultRow>> GetUnsentRowsAsync(CancellationToken cancellationToken = default)
    {
        var approvedRows = await GetApprovedRowsAsync(cancellationToken);
        var rejectedRows = await GetRejectedRowsAsync(cancellationToken);

        return approvedRows.Concat(rejectedRows).ToList();
    }

    private async Task<IReadOnlyList<CollateralResultRow>> GetApprovedRowsAsync(CancellationToken ct)
    {
        var raw = await (
            from e in db.CollateralEngagements.AsNoTracking()
            join m in db.CollateralMasters.AsNoTracking() on e.CollateralMasterId equals m.Id
            where m.HostCollateralId != null
                  && !m.IsDeleted
                  && !db.CollateralResultLogs.Any(l => l.AppraisalId == e.AppraisalId)
            select new ApprovedRawRow
            {
                AppraisalId = e.AppraisalId,
                HostCollateralId = m.HostCollateralId!,
                AppraisalNumber = e.AppraisalNumber,
                AppraisalValue = e.AppraisalValue,
                LandValue = e.LandValue,
                BuildingValue = e.BuildingValue,
                ForcedSaleValue = e.ForcedSaleValue,
                AppraisalDate = e.AppraisalDate,
                InternalAppraiserName = e.InternalAppraiserName,
                AppraisalCompanyName = e.AppraisalCompanyName,
                AppraisalCompanyCode = e.AppraisalCompanyCode,
                CollateralType = m.CollateralType,
                MachineLifeYear = m.MachineDetail != null ? m.MachineDetail.LifeYear : null
            })
            .ToListAsync(ct);

        return raw.Select(MapApproved).ToList();
    }

    private static CollateralResultRow MapApproved(ApprovedRawRow r)
    {
        int? lifeYear = null;
        if (r.CollateralType == CollateralTypes.Machine && r.MachineLifeYear is not null)
        {
            var rounded = (int)Math.Round(r.MachineLifeYear.Value, MidpointRounding.AwayFromZero);
            if (rounded is >= 0 and <= 999)
                lifeYear = rounded;
        }

        var appraisalDate = DateOnly.FromDateTime(r.AppraisalDate);

        return new CollateralResultRow(
            AppraisalId: r.AppraisalId,
            CollateralId: r.HostCollateralId,
            AppraisalReportNumber: r.AppraisalNumber,
            AppraisalValue: r.AppraisalValue,
            LandValue: r.LandValue,
            BuildingValue: r.BuildingValue,
            ForceSaleValue: r.ForcedSaleValue,
            CurrentAppraisalDate: appraisalDate,
            NextAppraisalDate: appraisalDate.AddYears(3),
            InternalValuerCode: null,
            InternalValuerName: r.InternalAppraiserName,
            ExternalValuerCode: r.AppraisalCompanyCode,
            ExternalValuerName: r.AppraisalCompanyName,
            LifeYear: lifeYear,
            AppraisalStatus: "A");
    }

    private async Task<IReadOnlyList<CollateralResultRow>> GetRejectedRowsAsync(CancellationToken ct)
    {
        var raw = await db.PendingCollateralResults
            .AsNoTracking()
            .Where(p => p.SentAt == null)
            .ToListAsync(ct);

        return raw.Select(MapRejected).ToList();
    }

    private static CollateralResultRow MapRejected(PendingCollateralResult r)
    {
        return new CollateralResultRow(
            AppraisalId: r.AppraisalId,
            CollateralId: r.HostCollateralId ?? string.Empty,
            AppraisalReportNumber: r.AppraisalNumber,
            AppraisalValue: null,
            LandValue: null,
            BuildingValue: null,
            ForceSaleValue: null,
            CurrentAppraisalDate: null,
            NextAppraisalDate: null,
            InternalValuerCode: null,
            InternalValuerName: null,
            ExternalValuerCode: null,
            ExternalValuerName: null,
            LifeYear: null,
            AppraisalStatus: "R");
    }

    private sealed class ApprovedRawRow
    {
        public Guid AppraisalId { get; init; }
        public string HostCollateralId { get; init; } = null!;
        public string AppraisalNumber { get; init; } = null!;
        public decimal? AppraisalValue { get; init; }
        public decimal? LandValue { get; init; }
        public decimal? BuildingValue { get; init; }
        public decimal? ForcedSaleValue { get; init; }
        public DateTime AppraisalDate { get; init; }
        public string? InternalAppraiserName { get; init; }
        public string? AppraisalCompanyName { get; init; }
        public string? AppraisalCompanyCode { get; init; }
        public string CollateralType { get; init; } = null!;
        public decimal? MachineLifeYear { get; init; }
    }
}

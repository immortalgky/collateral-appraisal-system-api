using Collateral.CollateralMasters.Models;
using Collateral.Contracts;
using Collateral.Contracts.FileInterface;
using Collateral.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data;

namespace Collateral.CollateralMasters.CollateralResult;

/// <summary>
/// Builds the outbound Collateral Result rows — <b>one row per collateral master</b>, carrying the
/// figures of that master's latest engagement.
///
/// AS400 keys collateral, not appraisals: it mints one id per collateral at drawdown and expects to
/// hold our current view of that collateral. The id therefore lives on
/// <c>CollateralMaster.HostCollateralId</c>, and the master is the unit of the file.
///
/// <b>Why the grain matters here specifically.</b> The `HostCollateralId IS NOT NULL` test is not
/// merely where the value comes from — it is the gate deciding which rows are ready to send. Keying
/// it to the master while still emitting one row per appraisal would make every never-sent older
/// appraisal of that master eligible at once, each stamped with the master's single id.
///
/// <c>CollateralResultLogs</c> stays keyed by AppraisalId, so a new appraisal produces exactly one
/// send and a re-run produces none.
///
/// <b>The four valuer fields are mutually exclusive.</b> An appraisal ran on the External path or the
/// Internal path, never both, so a record carries either the External pair or the Internal pair and
/// blanks the other — see <see cref="SelectValuerFields"/>.
/// </summary>
public class CollateralResultQuery(
    CollateralDbContext db,
    ISqlConnectionFactory connectionFactory,
    ILogger<CollateralResultQuery> logger) : ICollateralResultQuery
{
    /// <summary>IN-clause chunk size, matching HostCollateralLinkIngestor.</summary>
    private const int BatchSize = 1000;

    /// <summary>AS400 field width for InternalValuerCode (positions 107-110).</summary>
    private const int InternalValuerCodeWidth = 4;

    /// <summary>
    /// Engagements written by the AS400 legacy import. Excluded from the outbound file — see the
    /// filter below. Duplicated from <c>As400LegacyImporter.LegacyAppraisalType</c> rather than
    /// referenced, to keep this query free of a dependency on the importer.
    /// </summary>
    private const string LegacyAppraisalType = "AS400Legacy";

    /// <summary>
    /// Largest value the AreaUtilization field can carry (positions 202-208, implied dec(7,2)).
    /// Duplicated from <c>CollateralResultFileWriter.MaxAreaUtilization</c> — Collateral cannot
    /// reference the Integration module, which is the direction the dependency already runs.
    /// </summary>
    private const decimal MaxAreaUtilization = 99999.99m;

    /// <summary>Largest value the BuildingAge field can carry (positions 199-201, dec(3,0)).</summary>
    private const int MaxBuildingAge = 999;

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
            // Eligibility gate: no collateral id means AS400 has not created a collateral row for
            // this master (it mints one at drawdown), so there is nothing to key a result to.
            where m.HostCollateralId != null
                  && m.IsMaster
                  && !m.IsDeleted
                  // ...and this engagement is the master's latest SENDABLE one. One row per master,
                  // not per appraisal: AS400 holds collateral, and what it should hold is our
                  // current view of it. Without this an older appraisal that has never been sent
                  // would go out alongside the current one, both stamped with the master's single id.
                  //
                  // The legacy filter belongs INSIDE this subquery, not beside it. Legacy rows carry
                  // a valuation AS400 itself performed and sent us — echoing it back would report an
                  // appraisal we never made. But excluding them only from the outer filter picks the
                  // representative first and rejects it second: a master whose newest engagement
                  // happens to be a legacy row would then emit NOTHING, silently dropping collateral
                  // the bank holds from the file. Measured on a production-like set, an import made
                  // exactly that happen to 135 masters.
                  && e.Id == m.Engagements
                        .Where(x => x.AppraisalType != LegacyAppraisalType)
                        .OrderByDescending(x => x.AppraisalDate)
                        .ThenByDescending(x => x.CreatedAt)
                        .ThenByDescending(x => x.Id)
                        .Select(x => x.Id)
                        .FirstOrDefault()
                  // Keyed by AppraisalId, so each new appraisal produces exactly one send and a
                  // re-run produces none.
                  && !db.CollateralResultLogs.Any(log => log.AppraisalId == e.AppraisalId)
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
                AppraisalCompanyId = e.AppraisalCompanyId,
                AppraiserUserId = e.AppraiserUserId,
                CollateralType = m.CollateralType,
                MachineLifeYear = m.MachineDetail != null ? m.MachineDetail.LifeYear : null,
                // A leasehold master (LSL / LSB / LS / LSU) carries only its LeaseholdDetail — the
                // condo's age and area live on the UNDERLYING master it points at, since one
                // appraisal property yields two rows (the RE row and the lease row that owns the
                // engagement). Reading m.CondoDetail alone sent every leasehold record out with 000.
                CondoBuildingAge = m.LeaseholdDetail != null
                    ? db.CollateralMasters
                        .Where(u => u.Id == m.LeaseholdDetail.UnderlyingMasterId && u.CondoDetail != null)
                        .Select(u => u.CondoDetail!.BuildingAge)
                        .FirstOrDefault()
                    : m.CondoDetail != null ? m.CondoDetail.BuildingAge : null,
                CondoUsableArea = m.LeaseholdDetail != null
                    ? db.CollateralMasters
                        .Where(u => u.Id == m.LeaseholdDetail.UnderlyingMasterId && u.CondoDetail != null)
                        .Select(u => u.CondoDetail!.UsableArea)
                        .FirstOrDefault()
                    : m.CondoDetail != null ? m.CondoDetail.UsableArea : null,
                // Aggregated across every building on this engagement, not the Sequence=1 representative:
                // a title with a house plus an outbuilding must report the whole footprint, and the age
                // of the oldest structure is the one that drives the bank's depreciation view.
                BuildingsMaxAge = e.Buildings.Max(b => b.BuildingAge),
                BuildingsTotalArea = e.Buildings.Sum(b => b.BuildingArea)
            })
            .ToListAsync(ct);

        // Only the Internal path emits an InternalValuerCode, so only those rows need the employee-id
        // lookup — resolving it for an external engagement would cost a round-trip for a value that is
        // then blanked, and would raise the too-long warning below for a code nobody ever sends.
        var internalRows = raw.Where(r => !IsExternalEngagement(r.AppraisalCompanyId)).ToList();

        var employeeIds = await LoadEmployeeIdsAsync(
            internalRows.Select(r => r.AppraiserUserId).OfType<string>(), ct);

        var rows = raw.Select(r => MapApproved(r, employeeIds)).ToList();

        // One warning per run, not per row: an employee id that will not fit the 4-character AS400
        // field is sent blank, and blank is indistinguishable from "no id on file" in the output.
        var tooLong = internalRows
            .Where(r => r.AppraiserUserId is not null
                        && employeeIds.TryGetValue(r.AppraiserUserId, out var id)
                        && ToInternalValuerCode(id) is null
                        && !string.IsNullOrWhiteSpace(id))
            .Select(r => $"{r.AppraiserUserId}={employeeIds[r.AppraiserUserId!]}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (tooLong.Count > 0)
            logger.LogWarning(
                "[CollateralResultQuery] {Count} appraiser(s) have an EmployeeId that does not fit the "
                + "{Width}-character InternalValuerCode field even after stripping leading zeros; those rows "
                + "go out with a blank code rather than a truncated one. {Offenders}",
                tooLong.Count, InternalValuerCodeWidth, string.Join(", ", tooLong.Take(50)));

        return rows;
    }

    /// <summary>
    /// Maps <c>auth.AspNetUsers.EmployeeId</c> by <c>UserName</c>, because
    /// <c>CollateralEngagement.AppraiserUserId</c> holds a username rather than a Guid — the same join
    /// <c>GetAppraisalForCollateralQueryHandler</c> uses to resolve the appraiser's display name.
    /// Read with Dapper: <c>auth</c> belongs to another module's DbContext, so EF cannot join it.
    /// </summary>
    private async Task<Dictionary<string, string>> LoadEmployeeIdsAsync(
        IEnumerable<string> userNames, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var distinct = userNames.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
            return result;

        var connection = connectionFactory.GetOpenConnection();

        foreach (var chunk in distinct.Chunk(BatchSize))
        {
            const string sql = """
                SELECT UserName, EmployeeId
                FROM auth.AspNetUsers
                WHERE UserName IN @Names AND EmployeeId IS NOT NULL
                """;

            var rows = await connection.QueryAsync<(string UserName, string EmployeeId)>(
                new CommandDefinition(sql, new { Names = chunk }, cancellationToken: ct));

            foreach (var row in rows)
                result[row.UserName] = row.EmployeeId;
        }

        return result;
    }

    /// <summary>
    /// Turns an <c>EmployeeId</c> into the AS400 InternalValuerCode.
    ///
    /// The field is 4 characters while employee ids are 5, almost all of them zero-padded
    /// (<c>06327</c>), so the leading zeros come off first. Anything still too long returns null and is
    /// sent blank — <see cref="Integration.Contracts.FixedWidth.FixedWidthRecordBuilder"/> truncates
    /// left-aligned fields silently, and a truncated id (<c>81018</c> → <c>8101</c>) would name a
    /// different member of staff in the bank's core system. Blank is wrong; a wrong id is worse.
    ///
    /// Public so it can be unit-tested directly, following <c>HostCollateralLinkIngestor.PickWinningRecord</c>.
    /// </summary>
    public static string? ToInternalValuerCode(string? employeeId)
    {
        var trimmed = employeeId?.Trim().TrimStart('0');
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > InternalValuerCodeWidth ? null : trimmed;
    }

    /// <summary>
    /// True when the appraisal behind this engagement ran on the External path — an appraisal company
    /// produced the book.
    ///
    /// The test is "an appraisal company is attached", the same rule the rest of the system uses
    /// (<c>AppraisalAssignment.AssigneeCompanyId IS NOT NULL</c>, see <c>vw_AppraisalDetail</c>), read
    /// off the value frozen onto the engagement rather than re-joined to the appraisal schema, whose
    /// live assignment can have moved on since.
    ///
    /// Off-system engagements (the EXTO / <c>Offline</c> assignment method) count as External: the
    /// company did the work even though a bank staffer keyed the book in, and they carry the company id
    /// like any other external assignment.
    /// </summary>
    public static bool IsExternalEngagement(Guid? appraisalCompanyId) => appraisalCompanyId is not null;

    /// <summary>The four valuer fields of a Detail record, at most one pair populated.</summary>
    public readonly record struct ValuerFields(
        string? InternalCode,
        string? InternalName,
        string? ExternalCode,
        string? ExternalName);

    /// <summary>
    /// Picks the valuer pair the record is allowed to carry and blanks the other.
    ///
    /// Neither pair can be trusted to be null on its own. On the External path
    /// <c>CollateralEngagement.AppraiserUserId</c> / <c>InternalAppraiserName</c> are still populated —
    /// with the bank's follow-up officer, or (when the assignment has no follow-up staff) with the
    /// external company's own appraiser, because the upstream
    /// <c>GetAppraisalForCollateralQueryHandler</c> resolves them through an
    /// <c>AssigneeUserId ?? InternalAppraiserId ?? ExternalAppraiserId</c> chain that never consults
    /// AssignmentType. An off-system engagement fills both pairs outright. Sending both is wrong on
    /// either path, so the branch happens here, at the file boundary.
    ///
    /// Public so it can be unit-tested directly, following <see cref="ToInternalValuerCode"/>.
    /// </summary>
    public static ValuerFields SelectValuerFields(
        Guid? appraisalCompanyId,
        string? internalValuerCode,
        string? internalAppraiserName,
        string? appraisalCompanyCode,
        string? appraisalCompanyName) =>
        IsExternalEngagement(appraisalCompanyId)
            ? new ValuerFields(null, null, appraisalCompanyCode, appraisalCompanyName)
            : new ValuerFields(internalValuerCode, internalAppraiserName, null, null);

    /// <summary>
    /// True for the types whose buildings live on <c>CollateralEngagementBuildings</c>. Condos (U and
    /// its leasehold twin LSU) are deliberately absent: their age and area sit on the master's
    /// CondoDetails instead.
    /// </summary>
    private static bool HasEngagementBuildings(string collateralType) =>
        collateralType is CollateralTypes.LandWithBuilding
            or CollateralTypes.LeaseholdBuilding
            or CollateralTypes.LeaseholdWithBuilding;

    /// <summary>Freehold condo (U) and leasehold condo (LSU) — both read from CondoDetails.</summary>
    private static bool IsCondoType(string collateralType) =>
        collateralType is CollateralTypes.Condo or CollateralTypes.LeaseholdCondo;

    /// <summary>
    /// Building age for CCEBIL (positions 199-201) — the OLDEST building on the engagement for building
    /// types, CondoDetails.BuildingAge for a condo, blank for bare land and machinery.
    ///
    /// Out-of-range values return null, which the writer renders as 000: a single bad row must not abort
    /// the nightly run, the same rule LifeYear follows.
    ///
    /// Public so it can be unit-tested directly, following <see cref="ToInternalValuerCode"/>.
    /// </summary>
    public static int? ToBuildingAge(string collateralType, int? condoBuildingAge, int? buildingsMaxAge)
    {
        int? age = HasEngagementBuildings(collateralType) ? buildingsMaxAge
            : IsCondoType(collateralType) ? condoBuildingAge
            : null;

        return age is >= 0 and <= MaxBuildingAge ? age : null;
    }

    /// <summary>
    /// Area utilization for CCEARE (positions 202-208, dec(7,2)) in sq.m — the SUM of every building on
    /// the engagement for building types, CondoDetails.UsableArea for a condo, blank for bare land and
    /// machinery. Bare land reports nothing here on purpose: the field means usable floor area, and its
    /// land area is held in sq.wa, a different unit the host would silently misread.
    ///
    /// A total wider than the field returns null (rendered as zeros) rather than overflowing.
    /// </summary>
    public static decimal? ToAreaUtilization(string collateralType, decimal? condoUsableArea, decimal? buildingsTotalArea)
    {
        decimal? area = HasEngagementBuildings(collateralType) ? buildingsTotalArea
            : IsCondoType(collateralType) ? condoUsableArea
            : null;

        return area >= 0m && area <= MaxAreaUtilization ? area : null;
    }

    private static CollateralResultRow MapApproved(ApprovedRawRow r, Dictionary<string, string> employeeIds)
    {
        int? lifeYear = null;
        if (r.CollateralType == CollateralTypes.Machine && r.MachineLifeYear is not null)
        {
            var rounded = (int)Math.Round(r.MachineLifeYear.Value, MidpointRounding.AwayFromZero);
            if (rounded is >= 0 and <= 999)
                lifeYear = rounded;
        }

        var appraisalDate = DateOnly.FromDateTime(r.AppraisalDate);

        string? internalValuerCode = null;
        if (r.AppraiserUserId is not null && employeeIds.TryGetValue(r.AppraiserUserId, out var employeeId))
            internalValuerCode = ToInternalValuerCode(employeeId);

        var valuer = SelectValuerFields(
            r.AppraisalCompanyId,
            internalValuerCode,
            r.InternalAppraiserName,
            r.AppraisalCompanyCode,
            r.AppraisalCompanyName);

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
            InternalValuerCode: valuer.InternalCode,
            InternalValuerName: valuer.InternalName,
            ExternalValuerCode: valuer.ExternalCode,
            ExternalValuerName: valuer.ExternalName,
            LifeYear: lifeYear,
            AppraisalStatus: "A",
            BuildingAge: ToBuildingAge(r.CollateralType, r.CondoBuildingAge, r.BuildingsMaxAge),
            AreaUtilization: ToAreaUtilization(r.CollateralType, r.CondoUsableArea, r.BuildingsTotalArea));
    }

    private async Task<IReadOnlyList<CollateralResultRow>> GetRejectedRowsAsync(CancellationToken ct)
    {
        // A rejected appraisal has no CollateralEngagement and can never receive an AS400 id,
        // because AS400 mints ids at drawdown and a rejected appraisal never gets there.
        // The R row therefore goes out with a blank CCDCID, which AS400 accepts (AppraisalNumber joins).
        var raw = await db.PendingCollateralResults
            .AsNoTracking()
            .Where(p => p.SentAt == null)
            .Select(p => new RejectedRawRow
            {
                AppraisalId = p.AppraisalId,
                AppraisalNumber = p.AppraisalNumber,
                HostCollateralId = p.HostCollateralId
            })
            .ToListAsync(ct);

        return raw.Select(MapRejected).ToList();
    }

    private static CollateralResultRow MapRejected(RejectedRawRow r)
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
            AppraisalStatus: "R",
            BuildingAge: null,
            AreaUtilization: null);
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
        /// <summary>Non-null ⇔ the appraisal ran on the External path. See <see cref="IsExternalEngagement"/>.</summary>
        public Guid? AppraisalCompanyId { get; init; }
        /// <summary>Username, not a Guid — joins to auth.AspNetUsers.UserName.</summary>
        public string? AppraiserUserId { get; init; }
        public string CollateralType { get; init; } = null!;
        public decimal? MachineLifeYear { get; init; }
        /// <summary>Last-known condo age from the master; NULL for every other type.</summary>
        public int? CondoBuildingAge { get; init; }
        /// <summary>Last-known condo usable area from the master; NULL for every other type.</summary>
        public decimal? CondoUsableArea { get; init; }
        /// <summary>Oldest building on the engagement; NULL when the engagement has no buildings.</summary>
        public int? BuildingsMaxAge { get; init; }
        /// <summary>Total area of every building on the engagement; NULL when it has none.</summary>
        public decimal? BuildingsTotalArea { get; init; }
    }

    private sealed class RejectedRawRow
    {
        public Guid AppraisalId { get; init; }
        public string AppraisalNumber { get; init; } = null!;
        public string? HostCollateralId { get; init; }
    }
}

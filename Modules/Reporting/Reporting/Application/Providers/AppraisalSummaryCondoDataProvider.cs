using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles an <see cref="AppraisalSummaryModel"/> for FSD §2.1.3.2
/// "ใบสรุปรายงานการประเมิน – ห้องชุด (Condo)".
///
/// Common queries (Q1–Q14 + ColTypeMap) are delegated to
/// <see cref="AppraisalSummaryCommonLoader"/> (itself batched in Phase C).
///
/// Phase C — this provider batches its own 3 condo-specific queries into one
/// QueryMultiple call (single round-trip):
///   RS01  QC1  appraisal.CondoAppraisalDetails — first-property header
///   RS02  QC2  appraisal.CondoAppraisalAreaDetails — area breakdown rows
///   RS03  QC3  appraisal.PropertyGroupItems + CondoAppraisalDetails — per-group detail
/// </summary>
public sealed class AppraisalSummaryCondoDataProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalSummaryCondoDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "appraisal-summary-condo";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        // ── Common data (Q1–Q14 + ColTypeMap) ───────────────────────────────────
        var common = await AppraisalSummaryCommonLoader.LoadAsync(connection, appraisalId, cancellationToken);
        if (common is null)
            throw new NotFoundException("Appraisal", entityId);

        // ── Batch: 3 condo-specific result sets, single round-trip ───────────────
        const string batchSql = """
            -- RS01: QC1 — First-property condo detail
            SELECT TOP 1
                cad.RoomNumber,
                cad.FloorNumber,
                cad.NumberOfFloors,
                cad.CondoName,
                cad.BuildingNumber,
                cad.BuiltOnTitleNumber,
                cad.CondoRegistrationNumber,
                cad.UsableArea,
                cad.BuildingConditionType,
                cad.BuildingConditionTypeOther,
                COALESCE(ptBC.Description, cad.BuildingConditionTypeOther, cad.BuildingConditionType)
                    AS BuildingConditionDisplay,
                cad.BuildingAge,
                cad.OwnerName,
                cad.ObligationDetails,
                cad.HasObligation,
                cad.Soi,
                cad.Street                AS Road,
                cad.SubDistrict,
                cad.District,
                cad.Province,
                cad.LandOffice,
                cad.Latitude,
                cad.Longitude
            FROM appraisal.CondoAppraisalDetails cad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
            LEFT JOIN parameter.Parameters ptBC
                ON ptBC.[Group] = 'BuildingCondition'
               AND ptBC.[Language] = 'TH'
               AND ptBC.[Code] = cad.BuildingConditionType
               AND ptBC.IsActive = 1
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS02: QC2 — Area breakdown rows for all condo details
            SELECT
                cad.Id               AS CondoDetailId,
                cada.AreaDescription,
                cada.AreaSize
            FROM appraisal.CondoAppraisalDetails cad
            JOIN appraisal.CondoAppraisalAreaDetails cada ON cada.CondoAppraisalDetailsId = cad.Id
            JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY cad.Id, cada.AreaDescription;

            -- RS03: QC3 — Per-group condo detail rows
            SELECT
                pgi.PropertyGroupId,
                pgi.SequenceInGroup,
                cad.Id               AS CondoDetailId,
                cad.RoomNumber,
                cad.FloorNumber,
                cad.NumberOfFloors,
                cad.CondoName,
                cad.BuildingNumber,
                cad.BuiltOnTitleNumber,
                cad.CondoRegistrationNumber,
                cad.UsableArea,
                cad.BuildingAge,
                cad.OwnerName,
                cad.SubDistrict,
                cad.District,
                cad.Province,
                COALESCE(ptBC.Description, cad.BuildingConditionTypeOther, cad.BuildingConditionType)
                    AS BuildingConditionDisplay
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.CondoAppraisalDetails cad ON cad.AppraisalPropertyId = ap.Id
            LEFT JOIN parameter.Parameters ptBC
                ON ptBC.[Group] = 'BuildingCondition'
               AND ptBC.[Language] = 'TH'
               AND ptBC.[Code] = cad.BuildingConditionType
               AND ptBC.IsActive = 1
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, pgi.SequenceInGroup;
            """;

        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        FirstCondoRow? firstCondo;
        List<CondoAreaDetailRow> areaDetailRows;
        List<GroupCondoDetailRow> groupCondoRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, p))
        {
            // RS01
            firstCondo = await multi.ReadFirstOrDefaultAsync<FirstCondoRow>();

            // RS02
            areaDetailRows = (await multi.ReadAsync<CondoAreaDetailRow>()).ToList();

            // RS03
            groupCondoRows = (await multi.ReadAsync<GroupCondoDetailRow>()).ToList();
        }

        // Build condo-style address from first condo detail
        var collateralAddress = firstCondo is not null
            ? ThaiAddressFormatter.FormatCondo(
                roomNumber: firstCondo.RoomNumber,
                floorNumber: firstCondo.FloorNumber,
                buildingName: firstCondo.CondoName,
                soi: firstCondo.Soi,
                road: firstCondo.Road,
                subDistrict: firstCondo.SubDistrict,
                district: firstCondo.District,
                province: firstCondo.Province)
            : null;

        var gps = (firstCondo?.Latitude is not null && firstCondo.Longitude is not null)
            ? $"{firstCondo.Latitude:F6}, {firstCondo.Longitude:F6}"
            : null;

        var areasByCondoDetailId = areaDetailRows
            .GroupBy(r => r.CondoDetailId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var condoByGroup = groupCondoRows
            .GroupBy(r => r.PropertyGroupId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.SequenceInGroup).ToList());

        // ── Build per-group summary rows ─────────────────────────────────────────
        var summaryGroups = common.GroupRows.Select(g =>
        {
            condoByGroup.TryGetValue(g.GroupId, out var condoRows);
            condoRows ??= [];

            var detailParts = new List<string>();

            foreach (var c in condoRows)
            {
                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(c.RoomNumber))
                    parts.Add($"ห้องชุดเลขที่ {c.RoomNumber}");
                if (!string.IsNullOrWhiteSpace(c.FloorNumber))
                    parts.Add($"ชั้นที่ {c.FloorNumber}");
                if (!string.IsNullOrWhiteSpace(c.CondoName))
                    parts.Add(c.CondoName);
                if (!string.IsNullOrWhiteSpace(c.BuiltOnTitleNumber))
                    parts.Add($"ปลูกสร้างบนโฉนดเลขที่ {c.BuiltOnTitleNumber}");

                areasByCondoDetailId.TryGetValue(c.CondoDetailId, out var areas);
                if (areas is { Count: > 0 })
                {
                    var totalUsable = areas.Sum(a => a.AreaSize ?? 0m);
                    if (totalUsable > 0)
                        parts.Add($"พื้นที่ห้องชุด {totalUsable:0.##} ตารางเมตร");
                }
                else if (c.UsableArea.HasValue && c.UsableArea.Value > 0)
                {
                    parts.Add($"พื้นที่ห้องชุด {c.UsableArea:0.##} ตารางเมตร");
                }

                if (c.NumberOfFloors.HasValue)
                    parts.Add($"อาคารสูง {c.NumberOfFloors:0.##} ชั้น");
                if (c.BuildingAge.HasValue)
                    parts.Add($"อายุอาคาร {c.BuildingAge} ปี");
                if (!string.IsNullOrWhiteSpace(c.BuildingConditionDisplay))
                    parts.Add($"สภาพอาคาร{c.BuildingConditionDisplay}");

                var addressTail = ThaiAddressFormatter.FormatLandBuilding(
                    houseNumber: null, village: null, moo: null, soi: null, road: null,
                    subDistrict: c.SubDistrict, district: c.District, province: c.Province);
                if (!string.IsNullOrWhiteSpace(addressTail))
                    parts.Add(addressTail);

                if (parts.Count > 0)
                    detailParts.Add(string.Join(" ", parts));
            }

            var collateralDetails = detailParts.Count > 0 ? string.Join(" | ", detailParts) : null;

            var unitCount = condoRows.Count > 0 ? condoRows.Count : g.PropertyCount;
            var areaOrUnit = unitCount > 0 ? $"{unitCount:0.00}" : null;

            return new SummaryGroupRow
            {
                GroupNumber = g.GroupNumber,
                GroupName = g.GroupName,
                PropertyType = common.TranslateCollateralType(g.PropertyType),
                CollateralDetails = collateralDetails,
                AreaOrUnit = areaOrUnit,
                PricePerAreaOrUnit = null,
                AppraisalValue = g.GroupAppraisalValue,
                Condition = null,
                Remark = null
            };
        }).ToList();

        string? firstPropertyType = summaryGroups.FirstOrDefault()?.PropertyType;

        // ── Build model ──────────────────────────────────────────────────────────
        var model = new AppraisalSummaryModel
        {
            AppraisalBookNumber = common.AppraisalNumber,
            AppraisalDate = common.AppraisalDate,
            CustomerName = common.CustomerName,
            AoName = common.AoName,
            AppraisalPurpose = common.AppraisalPurpose,
            PropertyType = firstPropertyType,
            CollateralAddress = string.IsNullOrEmpty(collateralAddress) ? null : collateralAddress,
            AdministrativeDistrict = firstCondo?.SubDistrict,
            LandOffice = firstCondo?.LandOffice,
            OldAppraisalValue = null,
            Appraiser = common.Appraiser,
            LoanValue = common.LoanValue,
            Groups = summaryGroups,
            TotalAppraisalValue = common.TotalAppraisalValue,
            BuildingCoverageAmount = common.BuildingCoverageAmount,
            ForcedSaleValue = common.ForcedSaleValue,
            Condition = common.Condition,
            Remark = common.Remark,
            LandOwner = firstCondo?.OwnerName,
            EntryExitRights = null,
            BuildingOwner = null,
            LandCondition = null,
            Obligation = firstCondo?.ObligationDetails,
            CityPlan = null,
            Gps = gps,
            GovernmentAssessedValue = null,
            Utilization = null,
            MachineType = null,
            MarketDemandConditions = null,
            IsWqs = common.IsWqs,
            IsSaleGrid = common.IsSaleGrid,
            IsCost = common.IsCost,
            IsIncome = common.IsIncome,
            IsHypothesis = common.IsHypothesis,
            IsLeasehold = common.IsLeasehold,
            IsProfitRent = common.IsProfitRent,
            AppraiserComment = common.AppraiserComment,
            AppraisalStaffName = common.StaffName,
            AppraisalStaffPosition = common.StaffPosition,
            AppraisalCheckerName = common.CheckerName,
            AppraisalCheckerPosition = common.CheckerPosition,
            AppraisalVerifyName = common.VerifyName,
            AppraisalVerifyPosition = common.VerifyPosition,
            MeetingNumber = common.Review?.MeetingNo,
            MeetingDate = common.Review?.MeetingDate,
            ShowMeeting = common.ShowMeeting,
            ApproverDecisionApproved = common.ApproverDecisionApproved,
            Approvers = common.Approvers,
            ApproverSummaryComment = common.CommitteeOpinion
        };

        logger.LogDebug(
            "AppraisalSummaryCondo model assembled for appraisal {AppraisalId}: " +
            "{GroupCount} groups, {ApproverCount} approvers, showMeeting={ShowMeeting}",
            appraisalId, summaryGroups.Count, common.Approvers.Count, common.ShowMeeting);

        return model;
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class FirstCondoRow
    {
        public string? RoomNumber { get; init; }
        public string? FloorNumber { get; init; }
        public decimal? NumberOfFloors { get; init; }
        public string? CondoName { get; init; }
        public string? BuildingNumber { get; init; }
        public string? BuiltOnTitleNumber { get; init; }
        public string? CondoRegistrationNumber { get; init; }
        public decimal? UsableArea { get; init; }
        public string? BuildingConditionDisplay { get; init; }
        public int? BuildingAge { get; init; }
        public string? OwnerName { get; init; }
        public string? ObligationDetails { get; init; }
        public string? HasObligation { get; init; }
        public string? Soi { get; init; }
        public string? Road { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public string? LandOffice { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
    }

    private sealed class CondoAreaDetailRow
    {
        public Guid CondoDetailId { get; init; }
        public string? AreaDescription { get; init; }
        public decimal? AreaSize { get; init; }
    }

    private sealed class GroupCondoDetailRow
    {
        public Guid PropertyGroupId { get; init; }
        public int SequenceInGroup { get; init; }
        public Guid CondoDetailId { get; init; }
        public string? RoomNumber { get; init; }
        public string? FloorNumber { get; init; }
        public decimal? NumberOfFloors { get; init; }
        public string? CondoName { get; init; }
        public string? BuildingNumber { get; init; }
        public string? BuiltOnTitleNumber { get; init; }
        public string? CondoRegistrationNumber { get; init; }
        public decimal? UsableArea { get; init; }
        public int? BuildingAge { get; init; }
        public string? OwnerName { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public string? BuildingConditionDisplay { get; init; }
    }
}

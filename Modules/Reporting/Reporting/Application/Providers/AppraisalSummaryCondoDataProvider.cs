using System.Text.Json;
using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Services;
using Shared.Configuration;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles an <see cref="AppraisalSummaryModel"/> for FSD §2.1.3.2
/// "ใบสรุปรายงานการประเมิน – ห้องชุด (Condo)".
///
/// Common queries (Q1–Q14 + ColTypeMap) are delegated to
/// <see cref="AppraisalSummaryCommonLoader"/> (itself batched in Phase C).
///
/// Phase C — this provider batches its own condo-specific queries into one
/// QueryMultiple call (single round-trip):
///   RS01  QC1  appraisal.CondoAppraisalDetails — first-property header
///   RS02  QC2  appraisal.CondoAppraisalAreaDetails — area breakdown rows
///   RS03  QC3  appraisal.PropertyGroupItems + CondoAppraisalDetails — per-group detail
///   RS04       parameter.Parameters — LandEntranceExit code→Thai map (สิทธิทางเข้า-ออก)
///   RS05       parameter.Parameters — LandUse code→Thai map (สภาพการใช้ประโยชน์)
/// </summary>
public sealed class AppraisalSummaryCondoDataProvider(
    ISqlConnectionFactory connectionFactory,
    ISystemConfigurationReader configReader,
    ILogger<AppraisalSummaryCondoDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "appraisal-summary-condo";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        var forceSaleRateDefault = await configReader.GetDecimalAsync("ForceSaleRateDefaultPct", 70m, cancellationToken);

        // ── Common data (Q1–Q14 + ColTypeMap) ───────────────────────────────────
        var common = await AppraisalSummaryCommonLoader.LoadAsync(connection, appraisalId, forceSaleRateDefault, cancellationToken);
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
                CASE WHEN cad.BuildingConditionType = '99' AND NULLIF(cad.BuildingConditionTypeOther, '') IS NOT NULL
                     THEN cad.BuildingConditionTypeOther
                     ELSE COALESCE(ptBC.Description, cad.BuildingConditionTypeOther, cad.BuildingConditionType) END
                    AS BuildingConditionDisplay,
                cad.BuildingAge,
                cad.OwnerName,
                cad.ObligationDetails,
                cad.HasObligation,
                cad.Soi,
                cad.Street                AS Road,
                COALESCE(tsub.NameTh,  cad.SubDistrict) AS SubDistrict,
                COALESCE(tdist.NameTh, cad.District)    AS District,
                COALESCE(tprov.NameTh, cad.Province)    AS Province,
                COALESCE(pLandOffice.[description], cad.LandOffice) AS LandOffice,
                cad.Latitude,
                cad.Longitude,
                cad.LandEntranceExitType,
                cad.LandEntranceExitTypeOther,
                CASE WHEN cad.LandFillType = '99' AND NULLIF(cad.LandFillTypeOther, '') IS NOT NULL
                     THEN cad.LandFillTypeOther
                     ELSE COALESCE(pFill.Description, cad.LandFillTypeOther, cad.LandFillType) END
                    AS LandFillDisplay,
                COALESCE(pUrban.Description, cad.UrbanPlanningType) AS UrbanPlanningType,
                cad.LandUseType,
                cad.LandUseTypeOther,
                cad.GovernmentPricePerSqm,
                cad.GovernmentPrice
            FROM appraisal.CondoAppraisalDetails cad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
            LEFT JOIN parameter.Parameters ptBC
                ON ptBC.[Group] = 'BuildingCondition'
               AND ptBC.[Language] = 'TH'
               AND ptBC.[Code] = cad.BuildingConditionType
               AND ptBC.IsActive = 1
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = cad.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = cad.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = cad.SubDistrict
            LEFT JOIN parameter.Parameters pLandOffice
                ON pLandOffice.[group]    = 'LandOffice'
               AND pLandOffice.[language] = 'TH'
               AND pLandOffice.[isactive] = 1
               AND pLandOffice.[code]     = cad.LandOffice
            LEFT JOIN parameter.Parameters pFill
                ON pFill.[Group] = 'Landfill'
               AND pFill.[Language] = 'TH'
               AND pFill.[Code] = cad.LandFillType
               AND pFill.IsActive = 1
            LEFT JOIN parameter.Parameters pUrban
                ON pUrban.[Group] = 'TypeOfUrbanPlanning'
               AND pUrban.[Language] = 'TH'
               AND pUrban.[Code] = cad.UrbanPlanningType
               AND pUrban.IsActive = 1
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
            -- Order by creation sequence: CondoAppraisalAreaDetails has no Seq/CreatedOn
            -- column, but its Id is a Guid v7 whose canonical text form sorts by creation
            -- time (native uniqueidentifier sort does not).
            ORDER BY cad.Id, CONVERT(char(36), cada.Id);

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
                COALESCE(tsub.NameTh,  cad.SubDistrict) AS SubDistrict,
                COALESCE(tdist.NameTh, cad.District)    AS District,
                COALESCE(tprov.NameTh, cad.Province)    AS Province,
                CASE WHEN cad.BuildingConditionType = '99' AND NULLIF(cad.BuildingConditionTypeOther, '') IS NOT NULL
                     THEN cad.BuildingConditionTypeOther
                     ELSE COALESCE(ptBC.Description, cad.BuildingConditionTypeOther, cad.BuildingConditionType) END
                    AS BuildingConditionDisplay
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.CondoAppraisalDetails cad ON cad.AppraisalPropertyId = ap.Id
            LEFT JOIN parameter.Parameters ptBC
                ON ptBC.[Group] = 'BuildingCondition'
               AND ptBC.[Language] = 'TH'
               AND ptBC.[Code] = cad.BuildingConditionType
               AND ptBC.IsActive = 1
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = cad.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = cad.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = cad.SubDistrict
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, pgi.SequenceInGroup;

            -- RS04: LandEntranceExit code→Thai map (สิทธิทางเข้า-ออก, JSON code list)
            SELECT [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [group] = 'LandEntranceExit' AND [language] = 'TH' AND [isactive] = 1;

            -- RS05: LandUse code→Thai map (สภาพการใช้ประโยชน์, JSON code list)
            SELECT [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [group] = 'LandUse' AND [language] = 'TH' AND [isactive] = 1;
            """;

        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        FirstCondoRow? firstCondo;
        List<CondoAreaDetailRow> areaDetailRows;
        List<GroupCondoDetailRow> groupCondoRows;
        List<ParamRow> entranceExitParams;
        List<ParamRow> landUseParams;

        using (var multi = await connection.QueryMultipleAsync(batchSql, p))
        {
            // RS01
            firstCondo = await multi.ReadFirstOrDefaultAsync<FirstCondoRow>();

            // RS02
            areaDetailRows = (await multi.ReadAsync<CondoAreaDetailRow>()).ToList();

            // RS03
            groupCondoRows = (await multi.ReadAsync<GroupCondoDetailRow>()).ToList();

            // RS04
            entranceExitParams = (await multi.ReadAsync<ParamRow>()).ToList();

            // RS05
            landUseParams = (await multi.ReadAsync<ParamRow>()).ToList();
        }

        var entranceExitMap = ToParamMap(entranceExitParams);
        var landUseMap = ToParamMap(landUseParams);

        // สิทธิทางเข้า-ออก / สภาพการใช้ประโยชน์ — JSON code-list columns, code 99 = "other"
        // (paired *Other free text replaces the "อื่นๆ" description).
        string? entryExitRights = ParameterCodeFormatter.DecodeJsonArray(
            firstCondo?.LandEntranceExitType, firstCondo?.LandEntranceExitTypeOther, entranceExitMap);
        string? utilization = ParameterCodeFormatter.DecodeJsonArray(
            firstCondo?.LandUseType, firstCondo?.LandUseTypeOther, landUseMap);

        // ราคาประเมินราชการ — condo is priced per SQUARE METRE (not per sq-wa, unlike land).
        string? governmentPriceText = firstCondo?.GovernmentPricePerSqm is { } gpps
            ? $"ตารางเมตรละ {gpps:N2} บาท"
            : null;

        var gps = ThaiAddressFormatter.FormatGps(firstCondo?.Latitude, firstCondo?.Longitude);

        var areasByCondoDetailId = areaDetailRows
            .GroupBy(r => r.CondoDetailId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var condoByGroup = groupCondoRows
            .GroupBy(r => r.PropertyGroupId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.SequenceInGroup).ToList());

        // ── Build per-group summary rows (condo groups only) ─────────────────────
        var condoFamily = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "U", "LSU" };
        var summaryGroups = common.GroupRows
            .Where(g => g.PropertyType != null && condoFamily.Contains(g.PropertyType))
            .Select(g =>
        {
            condoByGroup.TryGetValue(g.GroupId, out var condoRows);
            condoRows ??= [];

            var detailParts = new List<string>();

            foreach (var c in condoRows)
            {
                var parts = new List<string>();

                // Room / floor / building number
                if (!string.IsNullOrWhiteSpace(c.RoomNumber))
                    parts.Add($"ห้องชุดเลขที่ {c.RoomNumber}");
                if (!string.IsNullOrWhiteSpace(c.FloorNumber))
                    parts.Add($"ชั้นที่ {c.FloorNumber}");
                if (!string.IsNullOrWhiteSpace(c.BuildingNumber))
                    parts.Add($"อาคารเลขที่ {c.BuildingNumber}");

                // Condo name / registration number
                if (!string.IsNullOrWhiteSpace(c.CondoName))
                    parts.Add($"ชื่ออาคารชุด {c.CondoName}");
                if (!string.IsNullOrWhiteSpace(c.CondoRegistrationNumber))
                    parts.Add($"ทะเบียนอาคารชุดเลขที่ {c.CondoRegistrationNumber}");

                // Built-on title deed + administrative location
                if (!string.IsNullOrWhiteSpace(c.BuiltOnTitleNumber))
                    parts.Add($"ปลูกสร้างบนโฉนดเลขที่ {c.BuiltOnTitleNumber}");
                var addressTail = ThaiAddressFormatter.FormatLandBuilding(
                    houseNumber: null, village: null, moo: null, soi: null, road: null,
                    subDistrict: c.SubDistrict, district: c.District, province: c.Province);
                if (!string.IsNullOrWhiteSpace(addressTail))
                    parts.Add(addressTail);

                // Areas: each component (room / balcony / …) then the total
                areasByCondoDetailId.TryGetValue(c.CondoDetailId, out var areas);
                if (areas is { Count: > 0 })
                {
                    foreach (var ar in areas)
                    {
                        if (ar.AreaSize is not { } sz || sz <= 0) continue;
                        // AreaDescription is the appraiser's own label from the property screen and
                        // already reads as one (e.g. "พื้นที่ภายในห้องชุด") — prefixing it here
                        // produced "พื้นที่พื้นที่ภายในห้องชุด". Only unlabelled rows need the word.
                        var label = string.IsNullOrWhiteSpace(ar.AreaDescription)
                            ? "พื้นที่"
                            : ar.AreaDescription!.Trim();
                        parts.Add($"{label} {sz:0.##} ตารางเมตร");
                    }
                    var totalArea = areas.Sum(a => a.AreaSize ?? 0m);
                    if (areas.Count > 1 && totalArea > 0)
                        parts.Add($"รวมพื้นที่ {totalArea:0.##} ตารางเมตร");
                }
                else if (c.UsableArea is { } ua && ua > 0)
                {
                    parts.Add($"พื้นที่ห้องชุด {ua:0.##} ตารางเมตร");
                }

                // Building height / age / condition
                if (c.NumberOfFloors.HasValue)
                    parts.Add($"อาคารสูง {c.NumberOfFloors:0.##} ชั้น");
                if (c.BuildingAge.HasValue)
                    parts.Add($"อายุอาคาร {c.BuildingAge} ปี");
                if (!string.IsNullOrWhiteSpace(c.BuildingConditionDisplay))
                    parts.Add($"สภาพอาคาร{c.BuildingConditionDisplay}");

                if (parts.Count > 0)
                    detailParts.Add(string.Join(" ", parts));
            }

            var collateralDetails = detailParts.Count > 0 ? string.Join(" | ", detailParts) : null;

            // Condo does not show a unit count in the พื้นที่ / จำนวน column (a bare "1.00"
            // is meaningless); the column is left blank.
            var areaOrUnit = (string?)null;

            return new SummaryGroupRow
            {
                GroupNumber = g.GroupNumber,
                GroupName = g.GroupName,
                PropertyType = "ห้องชุด",
                CollateralDetails = collateralDetails,
                AreaOrUnit = areaOrUnit,
                PricePerAreaOrUnit = null,
                AppraisalValue = g.GroupAppraisalValue,
                Condition = null,
                Remark = null
            };
        }).ToList();

        // วิธีการประเมิน — scoped to the methods of the condo groups actually shown.
        var methodFlags = AppraisalSummaryCommonLoader.FlagsForGroups(
            common.GroupMethodTypes,
            common.GroupRows.Where(g => g.PropertyType != null && condoFamily.Contains(g.PropertyType)).Select(g => g.GroupId));

        // ── Build model ──────────────────────────────────────────────────────────
        var model = new AppraisalSummaryModel
        {
            AppraisalBookNumber = common.AppraisalNumber,
            AppraisalDate = common.AppraisalDate,
            CustomerName = common.CustomerName,
            AoName = common.AoName,
            AppraisalPurpose = common.AppraisalPurpose,
            // Condo form: property type is fixed (header + appraiser opinion).
            PropertyType = "ห้องชุด",
            SummaryPropertyType = "ห้องชุด",
            CollateralAddress = common.CollateralAddress,
            AdministrativeDistrict = firstCondo?.SubDistrict,
            LandOffice = firstCondo?.LandOffice,
            OldAppraisalValue = common.PrevAppraisedValue,
            HasPrevAppraisal = common.HasPrevAppraisal,
            IsReAppraisal = string.Equals(common.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase),
            IsIncreaseLimit = common.IsIncreaseLimit,
            ExistingLoanValue = common.ExistingLoanValue,
            Appraiser = common.Appraiser,
            LoanValue = common.LoanValue,
            Groups = summaryGroups,
            TotalAppraisalValue = summaryGroups.Count > 0 ? summaryGroups.Sum(g => g.AppraisalValue ?? 0m) : common.TotalAppraisalValue,
            BuildingCoverageAmount = common.BuildingCoverageAmount,
            ForcedSaleValue = common.ForcedSaleValue,
            Condition = common.Condition,
            Remark = common.Remark,
            LandOwner = firstCondo?.OwnerName,
            EntryExitRights = entryExitRights,
            BuildingOwner = null,
            LandCondition = firstCondo?.LandFillDisplay,
            LandConditions = string.IsNullOrWhiteSpace(firstCondo?.LandFillDisplay)
                ? []
                : [firstCondo!.LandFillDisplay!],
            Obligation = firstCondo?.ObligationDetails,
            CityPlan = firstCondo?.UrbanPlanningType,
            Gps = gps,
            GovernmentAssessedValue = firstCondo?.GovernmentPrice,
            GovernmentPriceText = governmentPriceText,
            Utilization = utilization,
            Utilizations = string.IsNullOrWhiteSpace(utilization) ? [] : [utilization!],
            MachineType = null,
            MarketDemandConditions = null,
            IsWqs = methodFlags.IsWqs,
            IsSaleGrid = methodFlags.IsSaleGrid,
            IsCost = methodFlags.IsCost,
            IsIncome = methodFlags.IsIncome,
            IsHypothesis = methodFlags.IsHypothesis,
            IsLeasehold = methodFlags.IsLeasehold,
            IsProfitRent = methodFlags.IsProfitRent,
            AppraiserComment = common.AppraiserComment,
            AppraisalStaffName = common.StaffName,
            AppraisalStaffPosition = common.StaffPosition,
            AppraisalCheckerName = common.CheckerName,
            AppraisalCheckerPosition = common.CheckerPosition,
            AppraisalVerifyName = common.VerifyName,
            AppraisalVerifyPosition = common.VerifyPosition,
            MeetingNumber = common.Review?.MeetingNo,
            MeetingDate = common.Review?.MeetingDate,
            ApprovalDate = common.ApprovalDate,
            IsCompleted = common.IsCompleted,
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
        public string? LandEntranceExitType { get; init; }
        public string? LandEntranceExitTypeOther { get; init; }
        public string? LandFillDisplay { get; init; }
        public string? UrbanPlanningType { get; init; }
        public string? LandUseType { get; init; }
        public string? LandUseTypeOther { get; init; }
        public decimal? GovernmentPricePerSqm { get; init; }
        public decimal? GovernmentPrice { get; init; }
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

    private sealed class ParamRow
    {
        public string? Code { get; init; }
        public string? Description { get; init; }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────
    // Code/array translation lives in ParameterCodeFormatter — this provider's copy was the
    // original and the only one that handled code 99 correctly.

    private static Dictionary<string, string?> ToParamMap(IEnumerable<ParamRow> rows) =>
        rows.Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);

}

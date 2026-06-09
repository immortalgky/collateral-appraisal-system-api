using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles an <see cref="AppraisalSummaryModel"/> for FSD §2.1.4
/// "สรุปรายงานการตรวจงานก่อสร้าง" (Construction Inspection Summary).
///
/// Common queries (Q1–Q14 + ColTypeMap) are delegated to
/// <see cref="AppraisalSummaryCommonLoader"/> (itself batched in Phase C).
///
/// Phase C — BuildAsync batches all 6 construction-specific queries into one
/// QueryMultiple call (single round-trip):
///   RS01  QCI1  ConstructionInspections aggregate — CI totals, progress pcts, remark
///   RS02  QCI2  PricingFinalValues — land appraisal value
///   RS03  QCI3  BuildingDepreciationDetails — non-CI building cost
///   RS04  QCI4  BuildingAppraisalDetails — first building name
///   RS05  QCI5  LandAppraisalDetails — first land address
///   RS06  QCI6  Appraisals (self + prev join) — PrevAppraisalId + prior number/type
///
/// Column provenance (verified against EF configs):
///   ConstructionInspections (appraisal.ConstructionInspections):
///     IsFullDetail (bit), TotalValue (decimal), SummaryPreviousProgressPct (decimal),
///     SummaryCurrentProgressPct (decimal), SummaryCurrentValue (decimal), Remark (nvarchar),
///     FileName (nvarchar — null when no document attached), AppraisalPropertyId (FK)
///   ConstructionWorkDetails (appraisal.ConstructionWorkDetails):
///     ConstructionInspectionId (FK), PreviousPropertyValue (decimal), CurrentPropertyValue (decimal)
///   BuildingDepreciationDetails (appraisal.BuildingDepreciationDetails):
///     BuildingAppraisalDetailId (FK), PriceAfterDepreciation (decimal)
///   PricingFinalValues (appraisal.PricingFinalValues):
///     PricingMethodId → PricingAnalysisMethods → PricingAnalysisApproaches → PricingAnalysis
///     (SubjectType=0, AnchorId=PropertyGroupId). LandValue (decimal).
///   BuildingAppraisalDetails (appraisal.BuildingAppraisalDetails):
///     PropertyName (nvarchar), AppraisalPropertyId (FK)
///   LandAppraisalDetails (appraisal.LandAppraisalDetails):
///     HouseNumber, Village, Soi, Street(Road), SubDistrict, District, Province, LandOffice
///   Appraisals (appraisal.Appraisals):
///     PrevAppraisalId (uniqueidentifier nullable), AppraisalNumber, AppraisalType
///
/// Deferred (no stored source — left null/false with inline comments):
///   InspectionRound, InstallmentNumber — no dedicated column exists in ConstructionInspections.
///   HasConstructionLicense — no document-type discriminator in ConstructionInspections; deferred.
///   HasConstructionPhoto   — no document-type discriminator in ConstructionInspections; deferred.
/// </summary>
public sealed class AppraisalSummaryConstructionDataProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalSummaryConstructionDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "appraisal-summary-construction";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();
        var model = await BuildAsync(connection, appraisalId, cancellationToken);

        logger.LogDebug(
            "AppraisalSummaryConstruction model assembled for appraisal {AppraisalId}: " +
            "ciTotal={CITotal:N2}, ciCurrent={CICurrent:N2}, " +
            "landValue={LandValue:N2}, nonCiBuilding={NonCiBuilding:N2}, " +
            "hasProgressTable={HasProgressTable}, isReferAppraisalBook={IsReferAppraisalBook}, " +
            "isReferConstructionBook={IsReferConstructionBook}",
            appraisalId,
            model.TotalLandBuilding100 ?? 0m,
            model.TotalLandCurrentBuilding ?? 0m,
            model.LandAppraisalValue ?? 0m,
            0m,
            model.HasProgressTable,
            model.IsReferAppraisalBook,
            model.IsReferConstructionBook);

        return model;
    }

    /// <summary>
    /// Builds an <see cref="AppraisalSummaryModel"/> from an open connection.
    /// Called by <see cref="GetModelAsync"/> (standalone summary) and by
    /// <c>InternalConstructionReportProvider</c> (composite internal report).
    /// </summary>
    internal static async Task<AppraisalSummaryModel> BuildAsync(
        System.Data.IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct)
    {
        // ── Common data (Q1–Q14 + ColTypeMap) ───────────────────────────────────
        var common = await AppraisalSummaryCommonLoader.LoadAsync(connection, appraisalId, ct);
        if (common is null)
            throw new NotFoundException("Appraisal", appraisalId.ToString());

        // ── Batch: 6 construction-specific result sets, single round-trip ─────────
        const string batchSql = """
            -- RS01: QCI1 — Construction inspection aggregate
            SELECT
                ISNULL(SUM(ci.TotalValue), 0)                                                AS CITotalValue,
                ISNULL(SUM(
                    CASE WHEN ci.IsFullDetail = 0
                         THEN ISNULL(ci.SummaryCurrentValue, 0)
                         ELSE ISNULL(wd_agg.CurrentPropertyValueSum, 0)
                    END
                ), 0)                                                                        AS CICurrentValue,
                ISNULL(SUM(ci.SummaryPreviousProgressPct), 0)                               AS SumPreviousProgressPct,
                ISNULL(SUM(ci.SummaryCurrentProgressPct), 0)                               AS SumCurrentProgressPct,
                MAX(CASE WHEN ci.FileName IS NOT NULL THEN 1 ELSE 0 END)                    AS HasDocument,
                STRING_AGG(CASE WHEN ci.Remark IS NOT NULL AND LEN(ci.Remark) > 0
                                THEN ci.Remark ELSE NULL END, ' / ')
                    WITHIN GROUP (ORDER BY ci.Id)                                           AS CombinedRemark
            FROM appraisal.ConstructionInspections ci
            JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
            LEFT JOIN (
                SELECT wd.ConstructionInspectionId,
                       SUM(wd.CurrentPropertyValue)  AS CurrentPropertyValueSum
                FROM appraisal.ConstructionWorkDetails wd
                GROUP BY wd.ConstructionInspectionId
            ) wd_agg ON wd_agg.ConstructionInspectionId = ci.Id
            WHERE ap.AppraisalId = @AppraisalId;

            -- RS02: QCI2 — Land appraisal value from PricingFinalValues
            SELECT ISNULL(SUM(pfv.LandValue), 0)
            FROM appraisal.PricingFinalValues pfv
            JOIN appraisal.PricingAnalysisMethods pam ON pam.Id = pfv.PricingMethodId
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId
            JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId
                AND pa.SubjectType = 0
            JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
            WHERE pg.AppraisalId = @AppraisalId;

            -- RS03: QCI3 — Non-CI building depreciation value
            SELECT ISNULL(SUM(bdd.PriceAfterDepreciation), 0)
            FROM appraisal.BuildingDepreciationDetails bdd
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
              AND NOT EXISTS (
                  SELECT 1 FROM appraisal.ConstructionInspections ci
                  WHERE ci.AppraisalPropertyId = ap.Id
              );

            -- RS04: QCI4 — First building name
            SELECT TOP 1 bad.PropertyName
            FROM appraisal.BuildingAppraisalDetails bad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS05: QCI5 — First land address
            SELECT TOP 1
                lad.HouseNumber,
                lad.Village,
                lad.Soi,
                lad.Street     AS Road,
                lad.SubDistrict,
                lad.District,
                lad.Province,
                lad.LandOffice
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS06: QCI6 — Prev appraisal (self + left join to prior)
            SELECT a.PrevAppraisalId,
                   prev.AppraisalNumber AS PrevAppraisalNumber,
                   prev.AppraisalType   AS PrevAppraisalType
            FROM appraisal.Appraisals a
            LEFT JOIN appraisal.Appraisals prev ON prev.Id = a.PrevAppraisalId
            WHERE a.Id = @AppraisalId
              AND a.IsDeleted = 0;
            """;

        var appraisalParams = new DynamicParameters();
        appraisalParams.Add("AppraisalId", appraisalId);

        CiAggregateRow? ciRow;
        decimal landAppraisalValue;
        decimal nonCiBuilding;
        string? buildingName;
        LandAddressRow? landAddr;
        PrevAppraisalRow? prevRow;

        using (var multi = await connection.QueryMultipleAsync(batchSql, appraisalParams))
        {
            // RS01
            ciRow = await multi.ReadFirstOrDefaultAsync<CiAggregateRow>();

            // RS02 — scalar decimal result set
            landAppraisalValue = await multi.ReadFirstOrDefaultAsync<decimal>();

            // RS03 — scalar decimal result set
            nonCiBuilding = await multi.ReadFirstOrDefaultAsync<decimal>();

            // RS04 — scalar string result set
            buildingName = await multi.ReadFirstOrDefaultAsync<string?>();

            // RS05
            landAddr = await multi.ReadFirstOrDefaultAsync<LandAddressRow>();

            // RS06
            prevRow = await multi.ReadFirstOrDefaultAsync<PrevAppraisalRow>();
        }

        var collateralAddress = landAddr is not null
            ? ThaiAddressFormatter.FormatLandBuilding(
                houseNumber: landAddr.HouseNumber,
                village: landAddr.Village,
                moo: null,
                soi: landAddr.Soi,
                road: landAddr.Road,
                subDistrict: landAddr.SubDistrict,
                district: landAddr.District,
                province: landAddr.Province)
            : null;

        // ── Refer-book logic via PrevAppraisalId ─────────────────────────────────
        bool isReferAppraisalBook = false;
        string? referAppraisalBookNumber = null;
        bool isReferConstructionBook = false;
        string? referConstructionBookNumber = null;

        if (prevRow?.PrevAppraisalId is not null && !string.IsNullOrWhiteSpace(prevRow.PrevAppraisalNumber))
        {
            bool isPriorProgressive = string.Equals(
                prevRow.PrevAppraisalType, "Progressive", StringComparison.OrdinalIgnoreCase);

            if (isPriorProgressive)
            {
                isReferConstructionBook = true;
                referConstructionBookNumber = prevRow.PrevAppraisalNumber;
            }
            else
            {
                isReferAppraisalBook = true;
                referAppraisalBookNumber = prevRow.PrevAppraisalNumber;
            }
        }

        // ── Derive construction value fields ──────────────────────────────────────
        decimal ciTotal   = ciRow?.CITotalValue    ?? 0m;
        decimal ciCurrent = ciRow?.CICurrentValue  ?? 0m;

        decimal? previousProgressPct = ciRow is not null ? ciRow.SumPreviousProgressPct : null;
        decimal? totalProgressPct = ciRow is not null ? ciRow.SumCurrentProgressPct : null;
        decimal? additionalProgressPct = (previousProgressPct.HasValue && totalProgressPct.HasValue)
            ? totalProgressPct.Value - previousProgressPct.Value
            : null;

        decimal? buildingValue100       = ciTotal > 0m ? ciTotal : null;
        decimal? currentBuildingValue   = (nonCiBuilding + ciCurrent) > 0m ? nonCiBuilding + ciCurrent : (decimal?)null;
        decimal? totalLandBuilding100   = (landAppraisalValue + nonCiBuilding + ciTotal) > 0m
            ? landAppraisalValue + nonCiBuilding + ciTotal : (decimal?)null;
        decimal? totalLandCurrentBuilding = (landAppraisalValue + nonCiBuilding + ciCurrent) > 0m
            ? landAppraisalValue + nonCiBuilding + ciCurrent : (decimal?)null;
        decimal? landAppraisalValueOut  = landAppraisalValue > 0m ? landAppraisalValue : (decimal?)null;

        bool hasProgressTable = (ciRow?.HasDocument ?? 0) == 1;

        bool hasConstructionLicense = false; // DEFERRED: no doc-type column in ConstructionInspections
        bool hasConstructionPhoto   = false; // DEFERRED: no doc-type column in ConstructionInspections

        string? inspectionRound   = null; // DEFERRED: no stored source
        string? installmentNumber = null; // DEFERRED: no stored source

        string? firstGroupType = common.GroupRows.FirstOrDefault()?.PropertyType;
        string? propertyType   = common.TranslateCollateralType(firstGroupType);

        decimal? reportTotalAppraisalValue = totalLandCurrentBuilding ?? common.TotalAppraisalValue;

        // ── Build model ──────────────────────────────────────────────────────────
        var model = new AppraisalSummaryModel
        {
            AppraisalBookNumber = common.AppraisalNumber,
            AppraisalDate       = common.AppraisalDate,
            CustomerName        = common.CustomerName,
            AoName              = common.AoName,
            AppraisalPurpose    = common.AppraisalPurpose,
            PropertyType        = propertyType,
            CollateralAddress   = string.IsNullOrEmpty(collateralAddress) ? null : collateralAddress,
            AdministrativeDistrict = landAddr?.SubDistrict,
            LandOffice          = landAddr?.LandOffice,
            OldAppraisalValue   = null,
            Appraiser           = common.Appraiser,
            LoanValue           = common.LoanValue,
            Groups              = [],
            TotalAppraisalValue = reportTotalAppraisalValue,
            BuildingCoverageAmount = common.BuildingCoverageAmount,
            ForcedSaleValue     = common.ForcedSaleValue,
            Condition           = common.Condition,
            Remark              = common.Remark,
            LandOwner           = null,
            EntryExitRights     = null,
            BuildingOwner       = null,
            LandCondition       = null,
            Obligation          = null,
            CityPlan            = null,
            Gps                 = null,
            GovernmentAssessedValue = null,
            Utilization         = null,
            MachineType         = null,
            MarketDemandConditions = null,

            // ── Construction-variant fields ──────────────────────────────────────
            IsReferAppraisalBook      = isReferAppraisalBook,
            ReferAppraisalBookNumber  = referAppraisalBookNumber,
            IsReferConstructionBook   = isReferConstructionBook,
            ReferConstructionBookNumber = referConstructionBookNumber,
            InspectionRound           = inspectionRound,
            InstallmentNumber         = installmentNumber,
            BuildingName              = buildingName,
            BuildingValue100          = buildingValue100,
            LandAppraisalValue        = landAppraisalValueOut,
            CurrentBuildingValue      = currentBuildingValue,
            TotalLandBuilding100      = totalLandBuilding100,
            TotalLandCurrentBuilding  = totalLandCurrentBuilding,
            PreviousProgressPct       = previousProgressPct,
            AdditionalProgressPct     = additionalProgressPct,
            TotalProgressPct          = totalProgressPct,
            HasConstructionLicense    = hasConstructionLicense,
            HasProgressTable          = hasProgressTable,
            HasConstructionPhoto      = hasConstructionPhoto,
            ConstructionRemark        = string.IsNullOrWhiteSpace(ciRow?.CombinedRemark)
                                            ? null : ciRow.CombinedRemark,

            // ── Pricing method flags ─────────────────────────────────────────────
            IsWqs        = common.IsWqs,
            IsSaleGrid   = common.IsSaleGrid,
            IsCost       = common.IsCost,
            IsIncome     = common.IsIncome,
            IsHypothesis = common.IsHypothesis,
            IsLeasehold  = common.IsLeasehold,
            IsProfitRent = common.IsProfitRent,

            // ── Appraiser / sign-off ─────────────────────────────────────────────
            AppraiserComment          = common.AppraiserComment,
            AppraisalStaffName        = common.StaffName,
            AppraisalStaffPosition    = common.StaffPosition,
            AppraisalCheckerName      = common.CheckerName,
            AppraisalCheckerPosition  = common.CheckerPosition,
            AppraisalVerifyName       = common.VerifyName,
            AppraisalVerifyPosition   = common.VerifyPosition,

            // ── Committee / approver block ───────────────────────────────────────
            MeetingNumber             = common.Review?.MeetingNo,
            MeetingDate               = common.Review?.MeetingDate,
            ShowMeeting               = common.ShowMeeting,
            ApproverDecisionApproved  = common.ApproverDecisionApproved,
            Approvers                 = common.Approvers,
            ApproverSummaryComment    = common.CommitteeOpinion
        };

        return model;
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class CiAggregateRow
    {
        /// <summary>SUM(ConstructionInspections.TotalValue) — building value at 100%.</summary>
        public decimal CITotalValue { get; init; }

        /// <summary>
        /// SUM(SummaryCurrentValue | ConstructionWorkDetails.CurrentPropertyValue) —
        /// building value at current progress (ciCurrent in BuildConstructionSummaryAsync).
        /// </summary>
        public decimal CICurrentValue { get; init; }

        /// <summary>SUM(SummaryPreviousProgressPct) — previous progress percentage.</summary>
        public decimal SumPreviousProgressPct { get; init; }

        /// <summary>SUM(SummaryCurrentProgressPct) — cumulative current progress percentage.</summary>
        public decimal SumCurrentProgressPct { get; init; }

        /// <summary>1 if any CI row has a non-null FileName; 0 otherwise.</summary>
        public int HasDocument { get; init; }

        /// <summary>STRING_AGG of non-empty CI Remark values.</summary>
        public string? CombinedRemark { get; init; }
    }

    private sealed class LandAddressRow
    {
        public string? HouseNumber { get; init; }
        public string? Village { get; init; }
        public string? Soi { get; init; }
        public string? Road { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public string? LandOffice { get; init; }
    }

    private sealed class PrevAppraisalRow
    {
        public Guid? PrevAppraisalId { get; init; }
        public string? PrevAppraisalNumber { get; init; }
        public string? PrevAppraisalType { get; init; }
    }
}

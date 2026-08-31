using System.Data;
using System.Globalization;
using Appraisal.Domain.Appraisals;
using Appraisal.Application.Features.Project.IssueUnitTicket;
using Appraisal.Domain.Projects;
using Dapper;
using MediatR;
using FluentValidation;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

public class GetAppraisalResultByNumberQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ISender sender
) : IQueryHandler<GetAppraisalResultByNumberQuery, GetAppraisalResultResponse?>
{
    public async Task<GetAppraisalResultResponse?> Handle(
        GetAppraisalResultByNumberQuery query,
        CancellationToken cancellationToken)
    {
        var conn = connectionFactory.GetOpenConnection();

        var p = new DynamicParameters();
        p.Add("AppraisalNumber", query.AppraisalNumber);
        var appraisal = await conn.QuerySingleOrDefaultAsync<AppraisalRow>(
            new CommandDefinition(GetAppraisalResultSql.ByAppraisalNumber, p, cancellationToken: cancellationToken));

        if (appraisal is null) return null;

        var selector = new UnitSelector(query.PlotNumber, query.RoomNumber, query.FloorNumber);
        // No case key on this route, so nothing identifies the caller beyond its token.
        return await AppraisalResultBuilder.BuildAsync(
            conn, sender, appraisal, selector, strict: true, issuedTo: null, cancellationToken);
    }
}

public class GetAppraisalResultsByCaseKeyQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ISender sender
) : IQueryHandler<GetAppraisalResultsByCaseKeyQuery, IReadOnlyList<GetAppraisalResultResponse>>
{
    public async Task<IReadOnlyList<GetAppraisalResultResponse>> Handle(
        GetAppraisalResultsByCaseKeyQuery query,
        CancellationToken cancellationToken)
    {
        var conn = connectionFactory.GetOpenConnection();

        var p = new DynamicParameters();
        p.Add("ExternalCaseKey", query.ExternalCaseKey);
        var appraisals = await conn.QueryAsync<AppraisalRow>(
            new CommandDefinition(GetAppraisalResultSql.ByExternalCaseKey, p, cancellationToken: cancellationToken));

        var selector = new UnitSelector(query.PlotNumber, query.RoomNumber, query.FloorNumber);
        var results = new List<GetAppraisalResultResponse>();
        foreach (var appraisal in appraisals)
        {
            var result = await AppraisalResultBuilder.BuildAsync(
                conn, sender, appraisal, selector, strict: false, issuedTo: query.ExternalCaseKey, cancellationToken);
            if (result is not null)
                results.Add(result);
        }

        return results;
    }
}

internal static class GetAppraisalResultSql
{
    // Lookup by appraisal number returns any status (PMA / in-progress appraisals are retrieved before
    // completion). The by-external-case-key path below still restricts to completed appraisals.
    public const string ByAppraisalNumber = """
                                            SELECT a.Id, a.AppraisalNumber, a.Status, a.Purpose, a.CompletedAt, a.RequestId,
                                                   -- SequenceOfApprove = the committee meeting number the appraisal was reviewed in
                                                   -- (latest). Same subquery as LegacyByAppraisalNumber so both endpoints agree.
                                                   (SELECT TOP 1 m.MeetingNo
                                                    FROM appraisal.AppraisalReviews ar
                                                    JOIN workflow.Meetings m ON m.Id = ar.MeetingId
                                                    WHERE ar.AppraisalId = a.Id
                                                    ORDER BY ar.ReviewedAt DESC) AS SequenceOfApprove,
                                                   a.AppraisalType,
                                                   rd.TotalSellingPrice AS MarketValue
                                            FROM appraisal.Appraisals a
                                            LEFT JOIN request.RequestDetails rd ON rd.RequestId = a.RequestId
                                            WHERE a.AppraisalNumber = @AppraisalNumber AND a.IsDeleted = 0
                                            """;

    public const string ByExternalCaseKey = """
                                            SELECT a.Id, a.AppraisalNumber, a.Status, a.Purpose, a.CompletedAt, a.RequestId,
                                                   -- SequenceOfApprove = the committee meeting number the appraisal was reviewed in
                                                   -- (latest). Same subquery as LegacyByAppraisalNumber so both endpoints agree.
                                                   (SELECT TOP 1 m.MeetingNo
                                                    FROM appraisal.AppraisalReviews ar
                                                    JOIN workflow.Meetings m ON m.Id = ar.MeetingId
                                                    WHERE ar.AppraisalId = a.Id
                                                    ORDER BY ar.ReviewedAt DESC) AS SequenceOfApprove,
                                                   a.AppraisalType,
                                                   rd.TotalSellingPrice AS MarketValue
                                            FROM appraisal.Appraisals a
                                            LEFT JOIN request.RequestDetails rd ON rd.RequestId = a.RequestId
                                            JOIN request.Requests r ON r.Id = a.RequestId
                                            WHERE r.ExternalCaseKey = @ExternalCaseKey AND a.IsDeleted = 0
                                              AND a.Status = 'Completed'
                                            ORDER BY a.CreatedAt DESC, a.AppraisalNumber
                                            """;

    // Legacy (AS400) header: adds AppraisalType and the request-level selling price (MarketValue).
    // The Province/District/SubDistrict returned to AS400 is the TITLE address, sourced per collateral
    // (land/condo detail, or the project for a block) — not the request-level DOPA address — so it is
    // resolved in the collateral/project queries below, not here.
    public const string LegacyByAppraisalNumber = """
                                            SELECT a.Id, a.AppraisalNumber, a.AppraisalType, a.Status, a.CompletedAt, a.RequestId,
                                                   rd.TotalSellingPrice AS MarketValue,
                                                   -- SequenceOfApprove = the committee meeting number the appraisal was reviewed in (latest).
                                                   (SELECT TOP 1 m.MeetingNo
                                                    FROM appraisal.AppraisalReviews ar
                                                    JOIN workflow.Meetings m ON m.Id = ar.MeetingId
                                                    WHERE ar.AppraisalId = a.Id
                                                    ORDER BY ar.ReviewedAt DESC) AS SequenceOfApprove
                                            FROM appraisal.Appraisals a
                                            LEFT JOIN request.RequestDetails rd ON rd.RequestId = a.RequestId
                                            WHERE a.AppraisalNumber = @AppraisalNumber AND a.IsDeleted = 0
                                            """;

    public const string ActiveAssignment = """
                                           SELECT TOP 1
                                               aa.Id AS AssignmentId,
                                               aa.AssignmentType,
                                               aa.AssigneeUserId,
                                               aa.AssigneeCompanyId,
                                               -- Thai-first. This SQL is shared by BOTH result endpoints, and both are JSON:
                                               --   POST /api/v1/appraisals/result  (LegacyAppraisalResultEnvelope)
                                               --   GET  /api/v2/appraisals/{no}/result
                                               -- Neither is fixed-width, so there is no column-width or byte-offset limit here.
                                               -- NOTE: the AS400 fixed-width ExternalValuerName is a DIFFERENT path — it reads
                                               -- the frozen collateral.CollateralEngagements.AppraisalCompanyName snapshot via
                                               -- Collateral/CollateralMasters/CollateralResult/CollateralResultQuery.cs, which
                                               -- stays English. Don't conflate the two.
                                               COALESCE(NULLIF(c.NameLocal, N''), c.Name) AS CompanyName,
                                               c.HostCompanyCode AS CompanyCode,
                                               u.FirstName AS UserFirstName,
                                               u.LastName  AS UserLastName,
                                               u.EmployeeId AS EmployeeId,
                                               appt.AppointmentDateTime
                                           FROM appraisal.AppraisalAssignments aa
                                           LEFT JOIN auth.Companies   c ON c.Id = TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier)
                                           LEFT JOIN auth.AspNetUsers u ON u.UserName = aa.AssigneeUserId
                                           OUTER APPLY (
                                               SELECT TOP 1 ap.AppointmentDateTime
                                               FROM appraisal.Appointments ap
                                               WHERE ap.AssignmentId = aa.Id AND ap.Status <> 'Cancelled'
                                               ORDER BY ap.AppointmentDateTime DESC
                                           ) appt
                                           WHERE aa.AppraisalId = @AppraisalId
                                             AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                                           ORDER BY aa.AssignedAt DESC, aa.CreatedAt DESC
                                           """;

    public const string Fee = """
                              SELECT TOP 1 af.TotalFeeAfterVAT
                              FROM appraisal.AppraisalFees af
                              WHERE af.AssignmentId = @AssignmentId
                              """;

    public const string ValuationTotals = """
                                          SELECT va.AppraisedValue, va.ForcedSaleValue, va.InsuranceValue, va.ValuationDate
                                          FROM appraisal.ValuationAnalyses va
                                          WHERE va.AppraisalId = @AppraisalId
                                          """;

    public const string GroupsAndCollaterals = """
                                               SELECT
                                                   pg.Id AS GroupId, pg.GroupName,
                                                   CAST(NULL AS decimal(18,2)) AS GroupAppraisedValue,
                                                   paa.ApproachType AS AppraisalMethod,
                                                   pfv.LandValue AS GroupLandValue,
                                                   pfv.BuildingValue AS GroupBuildingValue,
                                                   pfv.FinalValueAdjusted AS GroupUnitPrice,
                                                   pfv.ValuePerUnit AS GroupValuePerUnit,   -- selected method's per-Wa/Sqm rate → AppraisalValueWaOrM
                                                   ap.Id AS PropertyId, ap.PropertyType,
                                                   -- Land/LB fields (from LandAppraisalDetails + first LandTitle)
                                                   lad.Province, lad.District, lad.SubDistrict, lad.LandOffice,
                                                   lt.TitleNumber AS TitleNo, lt.LandParcelNumber AS LandNo,
                                                   lt.Rawang, lt.SurveyNumber AS SurveyNo,
                                                   lt.BookNumber AS BookNo, lt.PageNumber AS PageNo,
                                                   lt.AreaRai AS Rai, lt.AreaNgan AS Ngan, lt.AreaSquareWa AS Wa,
                                                   -- Building fields
                                                   bad.HouseNumber AS HouseNo, bad.BuildingType,
                                                   bad.BuildingAge, bad.NumberOfFloors AS TotalFloor,
                                                   -- Construction progress. bad.ConstructionCompletionPercent was dropped by migration
                                                   -- 20260821044547; the live value lives on the inspection, and the two modes are stored
                                                   -- and computed differently:
                                                   --   Summary     (IsFullDetail = 0) -> one keyed-in figure, ci.SummaryCurrentProgressPct.
                                                   --   Full Detail (IsFullDetail = 1) -> weighted rollup of the work items. The weight x
                                                   --       progress product is persisted per item as CurrentProportionPct (see
                                                   --       ConstructionWorkDetail.cs:63), so summing it gives the overall percent.
                                                   -- A COALESCE of the two would be wrong: Full Detail always leaves SummaryCurrentProgressPct
                                                   -- null, and a record switched from Summary to Full Detail keeps the stale summary figure.
                                                   --
                                                   -- The leading arm is this endpoint's own, and must NOT be copied into
                                                   -- GetDecisionSummaryQueryHandler.cs or IConstructionCurrentValueService.CiAggregateSql:
                                                   -- both inner-JOIN ConstructionInspections, so they only ever see inspected properties, and
                                                   -- for those internal screens "no inspection" correctly means "nothing to show". Here the
                                                   -- consumer is an external system that cannot tell "finished" from "not inspected yet", so a
                                                   -- building/condo flagged as not under construction reports 100%. Clearing the checkbox
                                                   -- DELETES the inspection row (UpdateBuildingPropertyCommandHandler.cs:96), which is why the
                                                   -- flag has to be read directly instead of inferred from ci. NULL is treated as false: the
                                                   -- column is nullable and several write paths never set it, and an unticked box and an
                                                   -- untouched one look the same on screen (GetAppraisalFeesQueryHandler.cs:37 reads it the
                                                   -- same way). The bad.Id/cad.Id guard keeps bare land, vehicles, vessels and machinery null
                                                   -- rather than 100 -- do not drop it. An inspection row wins over a stale flag.
                                                   --
                                                   -- Still not ISNULL'd to 0: a property under construction but not yet inspected stays null,
                                                   -- which is a different fact from "inspected at 0%". Note this column is PER PROPERTY, while
                                                   -- collateral.vw_RegulatoryExport reports the appraisal-level value-weighted
                                                   -- figure on every collateral row — for a multi-building appraisal the two files
                                                   -- differ by design, however identical the per-inspection CASE.
                                                   CASE WHEN ci.Id IS NULL
                                                         AND (bad.Id IS NOT NULL OR cad.Id IS NOT NULL)
                                                         AND COALESCE(bad.IsUnderConstruction, cad.IsUnderConstruction, 0) = 0
                                                        THEN 100
                                                        WHEN ci.IsFullDetail = 0
                                                        THEN ci.SummaryCurrentProgressPct
                                                        ELSE wdagg.CurrentProportionPctSum
                                                   END AS ConstructionPct,
                                                   -- Condo fields
                                                   cad.RoomNumber AS RoomNo, cad.FloorNumber AS FloorNo,
                                                   cad.BuildingNumber AS BuildingNo, cad.BuildingAge AS CondoBuildingAge,
                                                   cad.NumberOfFloors AS CondoTotalFloor, cad.UsableArea AS AreaUtilize,
                                                   cad.Province AS CadProvince, cad.District AS CadDistrict, cad.SubDistrict AS CadSubDistrict,
                                                   cad.LandOffice AS CadLandOffice,
                                                   -- Lease fields
                                                   leasd.ContractNo, leasd.LesseeName, leasd.LessorName,
                                                   -- Vehicle identity fields
                                                   vad.RegistrationNumber  AS VehicleRegistrationNo,
                                                   vad.Brand               AS VehicleBrand,
                                                   vad.Model               AS VehicleModel,
                                                   -- Vessel identity fields
                                                   vsad.RegistrationNumber AS VesselRegistrationNo,
                                                   vsad.VesselName         AS VesselName,
                                                   vsad.VesselType         AS VesselType,
                                                   -- Machinery identity fields
                                                   mad.MachineName         AS MachineName,
                                                   mad.Brand               AS MachineBrand,
                                                   mad.Model               AS MachineModel,
                                                   mad.SerialNo            AS MachineSerialNo,
                                                   -- Legacy-only descriptors (consumed by the AS400 variant)
                                                   bad.DecorationType      AS BuildingDecorationType,
                                                   cad.DecorationType      AS CondoDecorationType,
                                                   cad.CondoRegistrationNumber AS CondoRegistrationNumber,
                                                   cad.CondoName           AS CondoName,
                                                   -- Condo deed number moved to cad.TitleNumber; BuiltOnTitleNumber is the pre-rename fallback.
                                                   COALESCE(NULLIF(LTRIM(RTRIM(cad.TitleNumber)), ''), cad.BuiltOnTitleNumber) AS CondoBuiltOnTitleNo,
                                                   lad.Village             AS Village,
                                                   bad.TotalBuildingArea   AS TotalBuildingArea,
                                                   -- LandOffice code resolved to its Thai description (legacy variant only)
                                                   COALESCE(pLandOffice.[description],    lad.LandOffice) AS LandOfficeName,
                                                   COALESCE(pCadLandOffice.[description], cad.LandOffice) AS CadLandOfficeName,
                                                   -- Title-address geocodes resolved to Thai names (Title masters, NOT DOPA — legacy variant only)
                                                   COALESCE(ltProv.NameTh, lad.Province)    AS ProvinceName,
                                                   COALESCE(ltDist.NameTh, lad.District)    AS DistrictName,
                                                   COALESCE(ltSub.NameTh,  lad.SubDistrict) AS SubDistrictName,
                                                   COALESCE(ctProv.NameTh, cad.Province)    AS CadProvinceName,
                                                   COALESCE(ctDist.NameTh, cad.District)    AS CadDistrictName,
                                                   COALESCE(ctSub.NameTh,  cad.SubDistrict) AS CadSubDistrictName,
                                                   -- PMA / pre-completion prices stored directly on the property (no ValuationAnalyses yet)
                                                   ap.SellingPrice           AS PropSellingPrice,
                                                   ap.ForcedSalePrice        AS PropForcedSalePrice,
                                                   ap.BuildingInsurancePrice AS PropBuildingInsurancePrice
                                               -- Keyed on AppraisalProperties (not PropertyGroups) so UNGROUPED properties
                                               -- (e.g. PMA input) are still returned; grouping/pricing are LEFT JOINed.
                                               FROM appraisal.AppraisalProperties ap
                                               LEFT JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id
                                               LEFT JOIN appraisal.PropertyGroups pg ON pg.Id = pgi.PropertyGroupId
                                               LEFT JOIN appraisal.PricingAnalysis pa ON pa.AnchorId = pg.Id AND pa.SubjectType = 0
                                               OUTER APPLY (
                                                   SELECT TOP 1 ApproachType
                                                   FROM appraisal.PricingAnalysisApproaches
                                                   WHERE PricingAnalysisId = pa.Id AND IsSelected = 1
                                                   ORDER BY Id
                                               ) paa
                                               OUTER APPLY (
                                                   SELECT TOP 1 fv.LandValue, fv.BuildingValue, fv.FinalValueAdjusted,
                                                          pm.ValuePerUnit
                                                   FROM appraisal.PricingAnalysisApproaches pap
                                                   JOIN appraisal.PricingAnalysisMethods pm ON pm.ApproachId = pap.Id AND pm.IsSelected = 1
                                                   JOIN appraisal.PricingFinalValues fv ON fv.PricingMethodId = pm.Id
                                                   WHERE pap.PricingAnalysisId = pa.Id AND pap.IsSelected = 1
                                                   ORDER BY pm.Id
                                               ) pfv
                                               LEFT JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap.Id
                                               LEFT JOIN (
                                                   SELECT *, ROW_NUMBER() OVER (PARTITION BY LandAppraisalDetailId ORDER BY Id) AS rn
                                                   FROM appraisal.LandTitles
                                               ) lt ON lt.LandAppraisalDetailId = lad.Id AND lt.rn = 1
                                               LEFT JOIN appraisal.BuildingAppraisalDetails bad ON bad.AppraisalPropertyId = ap.Id
                                               LEFT JOIN appraisal.CondoAppraisalDetails cad ON cad.AppraisalPropertyId = ap.Id
                                               LEFT JOIN appraisal.VehicleAppraisalDetails vad ON vad.AppraisalPropertyId = ap.Id
                                               LEFT JOIN appraisal.VesselAppraisalDetails vsad ON vsad.AppraisalPropertyId = ap.Id
                                               LEFT JOIN appraisal.MachineryAppraisalDetails mad ON mad.AppraisalPropertyId = ap.Id
                                               LEFT JOIN appraisal.LeaseAgreementDetails leasd ON leasd.AppraisalPropertyId = ap.Id
                                               -- ConstructionInspections is 1:1 with AppraisalProperty (unique index on AppraisalPropertyId)
                                               -- and wdagg is pre-grouped, so neither join fans out the one-row-per-property shape.
                                               LEFT JOIN appraisal.ConstructionInspections ci ON ci.AppraisalPropertyId = ap.Id
                                               LEFT JOIN (
                                                   SELECT ConstructionInspectionId,
                                                          SUM(CurrentProportionPct) AS CurrentProportionPctSum
                                                   FROM appraisal.ConstructionWorkDetails
                                                   GROUP BY ConstructionInspectionId
                                               ) wdagg ON wdagg.ConstructionInspectionId = ci.Id
                                               LEFT JOIN parameter.Parameters pLandOffice
                                                   ON pLandOffice.[group] = 'LandOffice' AND pLandOffice.[language] = 'TH'
                                                  AND pLandOffice.[isactive] = 1 AND pLandOffice.[code] = lad.LandOffice
                                               LEFT JOIN parameter.Parameters pCadLandOffice
                                                   ON pCadLandOffice.[group] = 'LandOffice' AND pCadLandOffice.[language] = 'TH'
                                                  AND pCadLandOffice.[isactive] = 1 AND pCadLandOffice.[code] = cad.LandOffice
                                               -- Title-address masters (land + condo detail geocodes → Thai names)
                                               LEFT JOIN parameter.TitleProvinces    ltProv ON ltProv.Code = lad.Province
                                               LEFT JOIN parameter.TitleDistricts    ltDist ON ltDist.Code = lad.District
                                               LEFT JOIN parameter.TitleSubDistricts ltSub  ON ltSub.Code  = lad.SubDistrict
                                               LEFT JOIN parameter.TitleProvinces    ctProv ON ctProv.Code = cad.Province
                                               LEFT JOIN parameter.TitleDistricts    ctDist ON ctDist.Code = cad.District
                                               LEFT JOIN parameter.TitleSubDistricts ctSub  ON ctSub.Code  = cad.SubDistrict
                                               WHERE ap.AppraisalId = @AppraisalId
                                               ORDER BY pg.GroupNumber, pgi.SequenceInGroup, ap.Id
                                               """;

    // Latest VAL_REPORT document per code for the appraisal (one row per DocumentTypeCode,
    // newest by CreatedAt). DocumentId is the download identifier; the relative URL is built in C#.
    public const string Documents = """
                                    SELECT x.DocumentType, x.DocumentId
                                    FROM (
                                        SELECT ad.DocumentTypeCode AS DocumentType,
                                               ad.DocumentId,
                                               ROW_NUMBER() OVER (
                                                   PARTITION BY ad.DocumentTypeCode
                                                   ORDER BY ad.CreatedAt DESC, CONVERT(char(36), ad.Id) DESC
                                               ) AS rn
                                        FROM appraisal.AppraisalDocuments ad
                                        JOIN parameter.DocumentTypes dt ON dt.Code = ad.DocumentTypeCode
                                        WHERE ad.AppraisalId = @AppraisalId
                                          AND dt.Category = 'VAL_REPORT'
                                          AND dt.IsActive = 1
                                          AND ad.DocumentId IS NOT NULL
                                    ) x
                                    WHERE x.rn = 1
                                    ORDER BY x.DocumentType
                                    """;

    // A block/project appraisal has a row in appraisal.Projects (1:1 via AppraisalId).
    // ProjectType code: "U" (Condo) | "LB"/"L" (Land / Land&Building).
    public const string ProjectByAppraisalId = """
                                               SELECT TOP 1 p.Id AS ProjectId, p.ProjectType,
                                                      p.ProjectName, p.Developer, p.BuiltOnTitleDeedNumber,
                                                      COALESCE(pLandOffice.[description], p.LandOffice) AS LandOfficeName,
                                                      -- Block title address (project geocodes → Title masters, NOT DOPA)
                                                      COALESCE(ltProv.NameTh, p.Province)    AS ProvinceName,
                                                      COALESCE(ltDist.NameTh, p.District)    AS DistrictName,
                                                      COALESCE(ltSub.NameTh,  p.SubDistrict) AS SubDistrictName,
                                                      -- Raw codes as well: v2 reports the geocode, v1 the resolved Thai name.
                                                      p.Province    AS ProvinceCode,
                                                      p.District    AS DistrictCode,
                                                      p.SubDistrict AS SubDistrictCode,
                                                      p.LandOffice  AS LandOfficeCode
                                               FROM appraisal.Projects p
                                               LEFT JOIN parameter.Parameters pLandOffice
                                                   ON pLandOffice.[group] = 'LandOffice' AND pLandOffice.[language] = 'TH'
                                                  AND pLandOffice.[isactive] = 1 AND pLandOffice.[code] = p.LandOffice
                                               LEFT JOIN parameter.TitleProvinces    ltProv ON ltProv.Code = p.Province
                                               LEFT JOIN parameter.TitleDistricts    ltDist ON ltDist.Code = p.District
                                               LEFT JOIN parameter.TitleSubDistricts ltSub  ON ltSub.Code  = p.SubDistrict
                                               WHERE p.AppraisalId = @AppraisalId
                                               """;

    // Block unit lookup. Identity lives on appraisal.ProjectUnits; the per-unit appraised value
    // lives on appraisal.ProjectUnitPrices (LEFT JOIN — a unit may have no price row yet).
    private const string BlockUnitSelect = """
                                           SELECT
                                               pu.Id AS ProjectUnitId,
                                               pu.RoomNumber, pu.Floor, pu.TowerName,
                                               pu.PlotNumber, pu.HouseNumber, pu.NumberOfFloors, pu.LandArea,
                                               pu.UsableArea, pu.SellingPrice,
                                               pu.CondoRegistrationNumber AS UnitRoomNo,   -- actually the room number (column to be renamed)
                                               pt.CondoRegistrationNumber,                 -- tower-level condo registration → BuildingRegisterNo
                                               pt.NumberOfFloors AS TowerFloors,           -- total floors of the building → FloorNumber
                                               pt.BuildingAge    AS TowerBuildingAge,
                                               COALESCE(pm.DecorationType, pt.DecorationType) AS DecorationType,
                                               modelApproach.ApproachType AS ModelApproachType,   -- selected approach of the unit's model → MethodOfAppraisal
                                               pp.TotalAppraisalValueRounded, pp.ForceSellingPrice, pp.CoverageAmount
                                           FROM appraisal.ProjectUnits pu
                                           LEFT JOIN appraisal.ProjectUnitPrices pp ON pp.ProjectUnitId = pu.Id
                                           LEFT JOIN appraisal.ProjectTowers pt ON pt.Id = pu.ProjectTowerId
                                           LEFT JOIN appraisal.ProjectModels pm ON pm.Id = pu.ProjectModelId
                                           OUTER APPLY (
                                               -- Block pricing method lives on the model's PricingAnalysis (SubjectType = 1).
                                               SELECT TOP 1 papp.ApproachType
                                               FROM appraisal.PricingAnalysis pamdl
                                               JOIN appraisal.PricingAnalysisApproaches papp
                                                   ON papp.PricingAnalysisId = pamdl.Id AND papp.IsSelected = 1
                                               WHERE pamdl.AnchorId = pu.ProjectModelId AND pamdl.SubjectType = 1
                                               ORDER BY papp.Id
                                           ) modelApproach
                                           WHERE pu.ProjectId = @ProjectId
                                           """;

    // IN, not '=': LOS names every unit the collateral covers in one call, and two adjacent plots
    // pledged together are one collateral. Dapper expands the list into the IN clause.
    public const string BlockUnitByPlot = BlockUnitSelect + """

                                              AND pu.PlotNumber IN @PlotNumbers
                                          ORDER BY pu.SequenceNumber
                                          """;

    public const string BlockUnitByRoomFloor = BlockUnitSelect + """

                                                  AND (pu.CondoRegistrationNumber IN @RoomNumbers
                                                       OR pu.RoomNumber IN @RoomNumbers)
                                                  AND pu.Floor = @Floor
                                              ORDER BY pu.SequenceNumber
                                              """;
}

internal sealed record AppraisalRow(
    Guid Id,
    string AppraisalNumber,
    string? Status,
    string? Purpose,
    DateTime? CompletedAt,
    Guid RequestId,
    string? SequenceOfApprove,
    string? AppraisalType,
    decimal? MarketValue);

internal sealed record AssignmentRow(
    Guid AssignmentId,
    string? AssignmentType,
    string? AssigneeUserId,
    string? AssigneeCompanyId,
    string? CompanyName,
    string? CompanyCode,
    string? UserFirstName,
    string? UserLastName,
    string? EmployeeId,
    DateTime? AppointmentDateTime);

internal sealed record ValuationRow(
    decimal? AppraisedValue,
    decimal? ForcedSaleValue,
    decimal? InsuranceValue,
    DateTime? ValuationDate);

internal sealed record CollateralRow(
    Guid? GroupId,        // null for ungrouped properties (e.g. PMA input not assigned to a group)
    string? GroupName,
    decimal? GroupAppraisedValue,
    string? AppraisalMethod,
    decimal? GroupLandValue,
    decimal? GroupBuildingValue,
    decimal? GroupUnitPrice,
    decimal? GroupValuePerUnit,
    Guid PropertyId,
    string? PropertyType,
    string? Province,
    string? District,
    string? SubDistrict,
    string? LandOffice,
    string? TitleNo,
    string? LandNo,
    string? Rawang,
    string? SurveyNo,
    string? BookNo,
    string? PageNo,
    decimal? Rai,
    decimal? Ngan,
    decimal? Wa,
    string? HouseNo,
    string? BuildingType,
    int? BuildingAge,
    decimal? TotalFloor,
    decimal? ConstructionPct,
    string? RoomNo,
    string? FloorNo,
    string? BuildingNo,
    int? CondoBuildingAge,
    decimal? CondoTotalFloor,
    decimal? AreaUtilize,
    string? CadProvince,
    string? CadDistrict,
    string? CadSubDistrict,
    string? CadLandOffice,
    string? ContractNo,
    string? LesseeName,
    string? LessorName,
    string? VehicleRegistrationNo,
    string? VehicleBrand,
    string? VehicleModel,
    string? VesselRegistrationNo,
    string? VesselName,
    string? VesselType,
    string? MachineName,
    string? MachineBrand,
    string? MachineModel,
    string? MachineSerialNo,
    // Legacy-only descriptors
    string? BuildingDecorationType,
    string? CondoDecorationType,
    string? CondoRegistrationNumber,
    string? CondoName,
    string? CondoBuiltOnTitleNo,
    string? Village,
    decimal? TotalBuildingArea,
    string? LandOfficeName,
    string? CadLandOfficeName,
    // Title-address geocodes resolved to Thai names (land detail / condo detail)
    string? ProvinceName,
    string? DistrictName,
    string? SubDistrictName,
    string? CadProvinceName,
    string? CadDistrictName,
    string? CadSubDistrictName,
    // PMA / pre-completion prices stored on the property (used when ValuationAnalyses is absent)
    decimal? PropSellingPrice,
    decimal? PropForcedSalePrice,
    decimal? PropBuildingInsurancePrice);

// Legacy (AS400) appraisal header row: appraisal identity + type and the request-level MarketValue.
// The title address is resolved per collateral (see CollateralRow / ProjectRow), not here.
internal sealed record LegacyAppraisalRow(
    Guid Id,
    string AppraisalNumber,
    string? AppraisalType,
    string? Status,
    DateTime? CompletedAt,
    Guid RequestId,
    decimal? MarketValue,
    string? SequenceOfApprove);

internal sealed record DocumentRow(string? DocumentType, Guid DocumentId);

// Optional unit selector for block/project appraisals.
internal sealed record UnitSelector(string? PlotNumber, string? RoomNumber, string? FloorNumber);

internal sealed record ProjectRow(
    Guid ProjectId,
    string ProjectType,
    string? ProjectName = null,
    string? Developer = null,
    string? BuiltOnTitleDeedNumber = null,
    string? LandOfficeName = null,
    // Block title address resolved to Thai names
    string? ProvinceName = null,
    string? DistrictName = null,
    string? SubDistrictName = null,
    // The same address as raw geocodes - what v2 reports, where v1 reports the names above.
    string? ProvinceCode = null,
    string? DistrictCode = null,
    string? SubDistrictCode = null,
    string? LandOfficeCode = null);

internal sealed record BlockUnitRow(
    Guid ProjectUnitId,
    string? RoomNumber,
    int? Floor,
    string? TowerName,
    string? PlotNumber,
    string? HouseNumber,
    int? NumberOfFloors,
    decimal? LandArea,
    decimal? UsableArea,
    decimal? SellingPrice,
    string? UnitRoomNo,
    string? CondoRegistrationNumber,
    int? TowerFloors,
    int? TowerBuildingAge,
    string? DecorationType,
    string? ModelApproachType,
    decimal? TotalAppraisalValueRounded,
    decimal? ForceSellingPrice,
    decimal? CoverageAmount);

internal static class AppraisalResultBuilder
{
    public static async Task<GetAppraisalResultResponse?> BuildAsync(
        IDbConnection conn,
        ISender sender,
        AppraisalRow appraisal,
        UnitSelector selector,
        bool strict,
        string? issuedTo,
        CancellationToken cancellationToken)
    {
        // Set only on the block path — an ordinary appraisal is already keyed by its own number.
        string? ticketNumber = null;

        var assignmentParams = new DynamicParameters();
        assignmentParams.Add("AppraisalId", appraisal.Id);
        var assignment = await conn.QueryFirstOrDefaultAsync<AssignmentRow>(
            new CommandDefinition(GetAppraisalResultSql.ActiveAssignment, assignmentParams,
                cancellationToken: cancellationToken));

        decimal? fee = null;
        if (assignment is not null)
        {
            var feeParams = new DynamicParameters();
            feeParams.Add("AssignmentId", assignment.AssignmentId);
            fee = await conn.QueryFirstOrDefaultAsync<decimal?>(
                new CommandDefinition(GetAppraisalResultSql.Fee, feeParams, cancellationToken: cancellationToken));
        }

        var valParams = new DynamicParameters();
        valParams.Add("AppraisalId", appraisal.Id);
        var valuation = await conn.QueryFirstOrDefaultAsync<ValuationRow>(
            new CommandDefinition(GetAppraisalResultSql.ValuationTotals, valParams,
                cancellationToken: cancellationToken));

        // Detect block/project appraisal (1:1 row in appraisal.Projects).
        var projParams = new DynamicParameters();
        projParams.Add("AppraisalId", appraisal.Id);
        var project = await conn.QueryFirstOrDefaultAsync<ProjectRow>(
            new CommandDefinition(GetAppraisalResultSql.ProjectByAppraisalId, projParams,
                cancellationToken: cancellationToken));

        // Top-level totals default to the appraisal's ValuationAnalyses; a matched block unit
        // overrides them with its own per-unit value below.
        decimal? totalAppraisalValue = valuation?.AppraisedValue;
        decimal? forceSalePrice = valuation?.ForcedSaleValue;
        decimal? fireInsurance = valuation?.InsuranceValue;
        // v1 reports the request-level total; a matched block unit overrides it below.
        decimal? marketValue = appraisal.MarketValue;
        List<AppraisalResultGroup> groups;

        if (project is null)
        {
            // Normal appraisal: groups come from PropertyGroups → AppraisalProperties.
            var groupParams = new DynamicParameters();
            groupParams.Add("AppraisalId", appraisal.Id);
            var collateralRows = (await conn.QueryAsync<CollateralRow>(
                new CommandDefinition(GetAppraisalResultSql.GroupsAndCollaterals, groupParams,
                    cancellationToken: cancellationToken))).ToList();

            // PMA / pre-completion: no ValuationAnalyses row exists yet, so the appraisal-level
            // figures come from the prices keyed on each property - the same rule as the v1 AS400
            // result (GetLegacyAppraisalResultQueryHandler: `completed ? valuation : property price`).
            // v1 serves ONE selected collateral so it coalesces within a group; v2 returns the whole
            // appraisal, so the analogue is a sum across properties. That does not double-count: a
            // group's price sits on a single row (a combined LB/U row, or the one priced row of an
            // L+B pair), never on both.
            // SumOrNull keeps "nothing priced yet" as null instead of a real-looking 0.
            if (!IsCompleted(appraisal.Status))
            {
                totalAppraisalValue = SumOrNull(collateralRows, r => r.PropSellingPrice) ?? totalAppraisalValue;
                forceSalePrice = SumOrNull(collateralRows, r => r.PropForcedSalePrice) ?? forceSalePrice;
                fireInsurance = SumOrNull(collateralRows, r => r.PropBuildingInsurancePrice) ?? fireInsurance;
                marketValue = SumOrNull(collateralRows, r => r.PropSellingPrice) ?? marketValue;
            }

            groups = collateralRows
                // Key on the property when there is no group. GroupId is null for every
                // ungrouped property (PMA intake, before grouping happens), so grouping on it
                // alone collapsed all of them into one bucket that then reported a single
                // group's pricing for the lot - 21 unrelated collaterals in one group on
                // APP-20260221-64654182. Each ungrouped property is its own group instead.
                .GroupBy(r => r.GroupId ?? r.PropertyId)
                .Select(g =>
                {
                    var first = g.First();
                    var collaterals = g.Select(r => new AppraisalResultCollateral(
                        r.PropertyType,
                        // A condo carries its deed number on the condo detail. r.TitleNo is the LAND
                        // title (LandTitles.TitleNumber) and is always null on a condo property, so
                        // the deed came back blank for every condo. CondoBuiltOnTitleNo already
                        // resolves cad.TitleNumber with the pre-rename BuiltOnTitleNumber fallback.
                        r.TitleNo ?? r.CondoBuiltOnTitleNo,
                        r.LandNo,
                        r.Rawang,
                        r.SurveyNo,
                        r.BookNo,
                        r.PageNo,
                        r.Rai,
                        r.Ngan,
                        r.Wa,
                        NullIfBlank(r.Village),
                        r.HouseNo,
                        r.BuildingType,
                        r.BuildingAge ?? r.CondoBuildingAge,
                        r.TotalFloor ?? r.CondoTotalFloor,
                        r.ConstructionPct,
                        r.RoomNo,
                        r.FloorNo,
                        r.BuildingNo,
                        r.CondoRegistrationNumber,
                        NullIfBlank(r.CondoName),
                        // v1 reports the condo's usable area, or the building's gross area for a
                        // non-condo. cad is null on a building row and vice versa, so this picks
                        // whichever one the collateral actually carries.
                        r.AreaUtilize ?? r.TotalBuildingArea,
                        r.ContractNo,
                        r.LesseeName,
                        r.LessorName,
                        r.Province ?? r.CadProvince,
                        r.District ?? r.CadDistrict,
                        r.SubDistrict ?? r.CadSubDistrict,
                        r.LandOffice ?? r.CadLandOffice,
                        null, // projectName - block only
                        ParseDecorate(r.BuildingDecorationType ?? r.CondoDecorationType),
                        r.VehicleRegistrationNo,
                        r.VehicleBrand,
                        r.VehicleModel,
                        r.VesselRegistrationNo,
                        r.VesselName,
                        r.VesselType,
                        r.MachineName,
                        r.MachineBrand,
                        r.MachineModel,
                        r.MachineSerialNo
                    )).ToList();

                    return new AppraisalResultGroup(
                        first.GroupAppraisedValue,
                        NormalizeApproach(first.AppraisalMethod),
                        first.GroupLandValue,
                        first.GroupBuildingValue,
                        first.GroupUnitPrice,
                        first.GroupValuePerUnit,
                        collaterals);
                })
                .ToList();
        }
        else
        {
            // Block/project appraisal: no AppraisalProperty rows exist; resolve the units by selector.
            var units = await ResolveBlockUnitsAsync(conn, project, selector, strict, cancellationToken);
            if (units.Count == 0)
            {
                // strict (by-number) with a valid-but-unmatched selector → 404 (null response).
                // non-strict (by-caseKey) or an invalid selector → header-only (empty groups).
                if (strict) return null;
                groups = [];
            }
            else
            {
                groups = [BuildBlockGroup(project, units)];

                // Every figure below is the collateral's, summed across the rooms it covers — the
                // grain AS400 will hold it at. A block's fire insurance is the units' own coverage,
                // not the appraisal-level total across the whole development.
                totalAppraisalValue = SumOrNull(units, u => u.TotalAppraisalValueRounded);
                forceSalePrice = SumOrNull(units, u => u.ForceSellingPrice);
                marketValue = SumOrNull(units, u => u.SellingPrice) ?? marketValue;
                fireInsurance = SumOrNull(units, u => u.CoverageAmount) ?? fireInsurance;

                // The ticket is issued here, at the only moment the bank actually needs a key: when
                // it is asking for the figures it will lend against. Issuing at appraisal time would
                // number thousands of units nobody ever finances.
                ticketNumber = await IssueTicketAsync(sender, appraisal.Id, units, project, selector, issuedTo, cancellationToken);
            }
        }

        var docParams = new DynamicParameters();
        docParams.Add("AppraisalId", appraisal.Id);
        var docRows = await conn.QueryAsync<DocumentRow>(
            new CommandDefinition(GetAppraisalResultSql.Documents, docParams, cancellationToken: cancellationToken));

        var documents = docRows
            .Select(d => new AppraisalResultDocument(
                d.DocumentType,
                $"/documents/{d.DocumentId}/download?download=false"))
            .ToList();

        string? valuerName = null;
        string? valuerCode = null;
        string? appraisalSource = null;
        // Who valued it is only meaningful once the appraisal is completed - before that (PMA /
        // in progress) the assignment says who is expected to value it, not who did. v1 blanks the
        // same fields pre-completion (`completed ? SplitValuer(...) : new ValuerSplit()`), so v2
        // matches; the API's omit-null policy drops the keys instead of emitting "" as v1 does.
        if (assignment is not null && IsCompleted(appraisal.Status))
        {
            if (string.Equals(assignment.AssignmentType, AssignmentType.External.Code, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(assignment.CompanyName))
                {
                    valuerName = assignment.CompanyName;
                    // External valuer code = the company's host code; the internal branch below
                    // uses the appraiser's employee id.
                    valuerCode = assignment.CompanyCode;
                    appraisalSource = "E";
                }
            }
            else if (string.Equals(assignment.AssignmentType, AssignmentType.Internal.Code, StringComparison.OrdinalIgnoreCase))
            {
                var fullName = $"{assignment.UserFirstName} {assignment.UserLastName}".Trim();
                valuerName = string.IsNullOrWhiteSpace(fullName) ? null : fullName;
                valuerCode = assignment.EmployeeId;
                appraisalSource = "I";
            }
        }

        var appraisalDate =
            (valuation?.ValuationDate ?? assignment?.AppointmentDateTime ?? appraisal.CompletedAt)
            ?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new GetAppraisalResultResponse(
            // The ticket takes this field's place when one was issued, rather than riding alongside
            // it. LOS carries whatever lands here into AS400's CCSURV, so putting the ticket
            // anywhere else would need LOS to change which field it forwards - and CCSURV is the
            // one place the return path reads a key back out of. An ordinary appraisal has no
            // ticket and reports its own number, exactly as before.
            AppraisalNumber: ticketNumber ?? appraisal.AppraisalNumber,
            Status: appraisal.Status,
            AppraisalPurpose: appraisal.Purpose,
            AppraisalFee: fee,
            AppraisalSource: appraisalSource,
            ValuerName: valuerName,
            ValuerCode: valuerCode,
            // Both dates are the valuation date: ValuationAnalyses.ValuationDate, falling back to
            // the appointment and finally to CompletedAt. AppraisalDate previously led with the
            // appointment, which is blank for an off-system external engagement (no Appointment row)
            // and is the inspection slot rather than the valuation date.
            //
            // CompletedAt is the last resort, not decoration: a legacy/migrated appraisal can have
            // neither of the first two, and AS400 would then receive a blank appraisal date for a
            // completed appraisal. This endpoint only ever serves completed work, so it resolves.
            ValuationDate: appraisalDate,
            AppraisalDate: appraisalDate,
            TotalAppraisalValue: totalAppraisalValue,
            ForceSalePrice: forceSalePrice,
            FireInsurance: fireInsurance,
            Developer: project?.Developer,
            SequenceOfApprove: appraisal.SequenceOfApprove,
            AppraisalType: MapAppraisalType(appraisal.AppraisalType),
            MarketValue: marketValue,
            TicketNumber: ticketNumber,
            Groups: groups,
            Documents: documents);
    }

    private static bool IsCompleted(string? status) =>
        string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);

    // Sums a nullable money column, returning null when no row carries a value at all - so
    // "nothing priced" stays distinguishable from a genuine total of 0.
    private static decimal? SumOrNull(List<CollateralRow> rows, Func<CollateralRow, decimal?> pick) =>
        rows.Any(r => pick(r) is not null) ? rows.Sum(r => pick(r) ?? 0m) : null;

    // Legacy Decorate is the DecorationType code with the leading zero stripped ("01" -> 1,
    // "99" -> 99); null when the code is absent or not numeric. Shared with the v1 endpoint so the
    // two feeds cannot disagree.
    internal static int? ParseDecorate(string? code) =>
        int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    // Thai land area: 1 rai = 4 ngan = 400 sq.wa; 1 ngan = 100 sq.wa. Splits a total given in
    // sq.wa. Returns nulls when there is no area (a condo unit has no land of its own) so v2 can
    // tell "no land" from "zero rai"; v1 flattens the nulls to 0 for its no-null contract.
    // Shared with the v1 endpoint so one implementation does the arithmetic.
    internal static (decimal? Rai, decimal? Ngan, decimal? Wa) SplitSqWa(decimal? totalSqWa)
    {
        if (totalSqWa is not { } total || total <= 0m) return (null, null, null);

        var rai = Math.Floor(total / 400m);
        var afterRai = total - rai * 400m;
        var ngan = Math.Floor(afterRai / 100m);
        return (rai, ngan, afterRai - ngan * 100m);
    }

    // The group's selected pricing approach. An absent or unrecognised approach falls back to
    // Market, mirroring v1's MapMethod (`_ => 3`), so both feeds report the same thing - at the cost
    // of not distinguishing "no approach chosen yet" from a real Market choice. Shared with the v1
    // endpoint so the fallback cannot drift.
    internal static string NormalizeApproach(string? approachType) =>
        approachType?.Trim().ToLowerInvariant() switch
        {
            "cost" => "Cost",
            "income" => "Income",
            _ => "Market",
        };

    // Legacy AS400 encoding of the appraisal type; 0 = unknown. Shared with the v1 endpoint
    // (GetLegacyAppraisalResultQueryHandler) so the two feeds can never disagree on the code.
    internal static int MapAppraisalType(string? type) => type switch
    {
        AppraisalTypes.New => 1,
        AppraisalTypes.ReAppraisal => 2,
        AppraisalTypes.Progressive => 3,
        AppraisalTypes.PreAppraisal => 4,
        _ => 0,
    };

    // These name columns hold "" as often as NULL; both mean "no name", and v2 omits nulls.
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    // Resolves a single block/project unit by the selector. Throws ValidationException (→ 400) when
    // the selector is missing/wrong for the project type and strict is on; returns null (no match /
    // ignored selector) otherwise.
    /// <summary>
    /// Splits a selector value into the keys it names. LOS sends every unit of one collateral in a
    /// single value — "1999/13,1999/14" — because AS400 holds them as one collateral, so a comma
    /// list is a request for one thing, not several.
    /// </summary>
    internal static List<string> SplitKeys(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Resolves every unit the selector names.
    ///
    /// A key that matches nothing is an ERROR, never a smaller answer. Returning the units that did
    /// match would issue a ticket covering fewer rooms than the caller asked for, and AS400 would
    /// then hold a collateral worth less than the property behind it — a shortfall nobody would see
    /// until it mattered.
    /// </summary>
    internal static async Task<IReadOnlyList<BlockUnitRow>> ResolveBlockUnitsAsync(
        IDbConnection conn,
        ProjectRow project,
        UnitSelector selector,
        bool strict,
        CancellationToken cancellationToken)
    {
        var p = new DynamicParameters();
        p.Add("ProjectId", project.ProjectId);

        string sql;
        List<string> requested;

        if (ProjectType.IsCondoCode(project.ProjectType))
        {
            // Condo requires room number(s) + a numeric FloorNumber (ProjectUnits.Floor is int).
            requested = SplitKeys(selector.RoomNumber);
            if (requested.Count == 0 ||
                !int.TryParse(selector.FloorNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var floor))
            {
                if (strict)
                    throw new ValidationException(
                        "roomNumber and a numeric floorNumber are required for a condo block appraisal.");
                return [];
            }

            p.Add("RoomNumbers", requested);
            p.Add("Floor", floor);
            sql = GetAppraisalResultSql.BlockUnitByRoomFloor;
        }
        else // "L" / "LB" — the plot number is the PlanNo LOS names a house by.
        {
            requested = SplitKeys(selector.PlotNumber);
            if (requested.Count == 0)
            {
                if (strict)
                    throw new ValidationException(
                        "plotNumber is required for a land/building block appraisal.");
                return [];
            }

            p.Add("PlotNumbers", requested);
            sql = GetAppraisalResultSql.BlockUnitByPlot;
        }

        var rows = (await conn.QueryAsync<BlockUnitRow>(
            new CommandDefinition(sql, p, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
            return [];

        var unmatched = requested
            .Where(k => !rows.Any(r => MatchesKey(r, k, project)))
            .ToList();

        if (unmatched.Count > 0)
        {
            if (strict)
                throw new ValidationException(
                    $"No unit of this project matches: {string.Join(", ", unmatched)}. " +
                    "Every unit named must resolve before a ticket can be issued.");
            return [];
        }

        return rows;
    }

    /// <summary>Whether a resolved row is the one the caller named by <paramref name="key"/>.</summary>
    internal static bool MatchesKey(BlockUnitRow row, string key, ProjectRow project)
    {
        bool Same(string? a) => string.Equals(a?.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase);

        return ProjectType.IsCondoCode(project.ProjectType)
            ? Same(row.UnitRoomNo) || Same(row.RoomNumber)
            : Same(row.PlotNumber);
    }

    // Maps a resolved block unit into the shared group/collateral shape. Only fields that exist on
    // ProjectUnit/ProjectUnitPrice are populated; title/address/building descriptors have no per-unit
    // source and stay null.
    /// <summary>
    /// One group holding one collateral per unit the caller named, valued at their sum.
    ///
    /// The group is the collateral AS400 will create — two rooms bought together are one pledge with
    /// one value — while each room stays visible as its own entry so the caller can still see what
    /// it is made of. Summing into a single collateral instead would hide the rooms; emitting a
    /// group each would tell AS400 to create several.
    /// </summary>
    /// <summary>
    /// Sums a per-unit figure, or null when not one unit carries it. Distinguishing "nothing priced"
    /// from "priced at zero" matters: zero is a value the caller can act on, null is an answer we do
    /// not have.
    /// </summary>
    private static decimal? SumOrNull(IReadOnlyList<BlockUnitRow> units, Func<BlockUnitRow, decimal?> pick) =>
        units.Any(u => pick(u).HasValue) ? units.Sum(u => pick(u) ?? 0m) : null;

    /// <summary>
    /// Issues (or re-returns) the ticket for the resolved units.
    ///
    /// The failure is deliberately loud. This runs on a GET, so it is tempting to let a ticket
    /// problem pass and still answer with the figures — but the caller's next step is to create the
    /// collateral in AS400 with the ticket, and a result handed over without one leads to a
    /// collateral keyed to nothing.
    /// </summary>
    private static async Task<string> IssueTicketAsync(
        ISender sender,
        Guid appraisalId,
        IReadOnlyList<BlockUnitRow> units,
        ProjectRow project,
        UnitSelector selector,
        string? issuedTo,
        CancellationToken cancellationToken)
    {
        var requested = ProjectType.IsCondoCode(project.ProjectType)
            ? SplitKeys(selector.RoomNumber)
            : SplitKeys(selector.PlotNumber);

        // Record the key the caller actually named this unit by, not whichever column matched, so a
        // repeat pull with the same spelling resolves to the same ticket.
        var refs = units
            .Select(u => new TicketUnitRef(
                u.ProjectUnitId,
                requested.FirstOrDefault(k => MatchesKey(u, k, project)) ?? string.Empty))
            .Where(r => !string.IsNullOrEmpty(r.UnitKey))
            .ToList();

        var result = await sender.Send(new IssueUnitTicketCommand(appraisalId, refs, issuedTo), cancellationToken);
        return result.TicketNumber;
    }

    private static AppraisalResultGroup BuildBlockGroup(ProjectRow project, IReadOnlyList<BlockUnitRow> units)
    {
        var collaterals = units.Select(u => BuildBlockCollateral(project, u)).ToList();

        // Null only when not one unit has been priced; a partially priced set still reports what it has.
        decimal? total = units.Any(u => u.TotalAppraisalValueRounded.HasValue)
            ? units.Sum(u => u.TotalAppraisalValueRounded ?? 0m)
            : null;

        return new AppraisalResultGroup(
            AppraisalValue: total,
            AppraisalMethod: NormalizeApproach(units[0].ModelApproachType),
            LandValue: null,
            BuildingValue: null,
            // A block unit is priced outright by its model, not by an area rate.
            UnitPrice: null,
            ValuePerUnit: null,
            Collaterals: collaterals);
    }

    private static AppraisalResultCollateral BuildBlockCollateral(ProjectRow project, BlockUnitRow unit)
    {
        var isCondo = ProjectType.IsCondoCode(project.ProjectType);
        var (rai, ngan, wa) = SplitSqWa(unit.LandArea);

        var collateral = new AppraisalResultCollateral(
            CollateralType: project.ProjectType,
            // A block is built on the project's deed, not a per-unit one.
            TitleNo: project.BuiltOnTitleDeedNumber,
            LandNo: isCondo ? null : unit.PlotNumber,
            Rawang: null,
            SurveyNo: null,
            BookNo: null,
            PageNo: null,
            Rai: rai,
            Ngan: ngan,
            Wa: wa,
            HouseNo: isCondo ? null : unit.HouseNumber,
            BuildingType: null,
            BuildingAge: unit.TowerBuildingAge,
            // v1 reads the tower's floor count for both project types, falling back to the
            // unit's own; NumberOfFloors is null on a condo unit anyway.
            TotalFloor: unit.TowerFloors ?? unit.NumberOfFloors,
            ConstructionPct: null,
            // Deliberately UnitRoomNo first, as v1 does - even though pu.CondoRegistrationNumber
            // and pu.RoomNumber hold different values (CR-002 vs A-502), so this does not echo
            // back the roomNumber the caller selected with. Matching v1 was the explicit ask.
            RoomNo: isCondo ? unit.UnitRoomNo ?? unit.RoomNumber : null,
            FloorNo: isCondo ? unit.Floor?.ToString(CultureInfo.InvariantCulture) : null,
            BuildingNo: isCondo ? unit.TowerName : null,
            CondoRegistrationNumber: isCondo ? unit.CondoRegistrationNumber : null,
            AreaUtilize: unit.UsableArea,
            ContractNo: null,
            LesseeName: null,
            LessorName: null,
            Province: project.ProvinceCode,
            District: project.DistrictCode,
            SubDistrict: project.SubDistrictCode,
            LandOffice: project.LandOfficeCode,
            Village: null,
            CondoName: null,
            // The block's own name. v1 packs this into BuildingDetails alongside the two above.
            ProjectName: NullIfBlank(project.ProjectName),
            Decorate: ParseDecorate(unit.DecorationType),
            VehicleRegistrationNo: null,
            VehicleBrand: null,
            VehicleModel: null,
            VesselRegistrationNo: null,
            VesselName: null,
            VesselType: null,
            MachineName: null,
            MachineBrand: null,
            MachineModel: null,
            MachineSerialNo: null);

        return collateral;
    }
}

using System.Data;
using System.Globalization;
using Appraisal.Domain.Appraisals;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

public class GetAppraisalResultByNumberQueryHandler(
    ISqlConnectionFactory connectionFactory
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

        return await AppraisalResultBuilder.BuildAsync(conn, appraisal, cancellationToken);
    }
}

public class GetAppraisalResultsByCaseKeyQueryHandler(
    ISqlConnectionFactory connectionFactory
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

        var results = new List<GetAppraisalResultResponse>();
        foreach (var appraisal in appraisals)
            results.Add(await AppraisalResultBuilder.BuildAsync(conn, appraisal, cancellationToken));

        return results;
    }
}

internal static class GetAppraisalResultSql
{
    public const string ByAppraisalNumber = """
                                            SELECT a.Id, a.AppraisalNumber, a.Purpose, a.CompletedAt, a.RequestId
                                            FROM appraisal.Appraisals a
                                            WHERE a.AppraisalNumber = @AppraisalNumber AND a.IsDeleted = 0
                                            """;

    public const string ByExternalCaseKey = """
                                            SELECT a.Id, a.AppraisalNumber, a.Purpose, a.CompletedAt, a.RequestId
                                            FROM appraisal.Appraisals a
                                            JOIN request.Requests r ON r.Id = a.RequestId
                                            WHERE r.ExternalCaseKey = @ExternalCaseKey AND a.IsDeleted = 0
                                            ORDER BY a.CreatedAt DESC, a.AppraisalNumber
                                            """;

    public const string ActiveAssignment = """
                                           SELECT TOP 1
                                               aa.Id AS AssignmentId,
                                               aa.AssignmentType,
                                               aa.AssigneeUserId,
                                               aa.AssigneeCompanyId,
                                               c.Name AS CompanyName,
                                               c.HostCompanyCode AS CompanyCode,
                                               u.FirstName AS UserFirstName,
                                               u.LastName  AS UserLastName,
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
                                          SELECT va.AppraisedValue, va.ForcedSaleValue, va.InsuranceValue, va.MarketValue, va.ValuationDate
                                          FROM appraisal.ValuationAnalyses va
                                          WHERE va.AppraisalId = @AppraisalId
                                          """;

    public const string GroupsAndCollaterals = """
                                               SELECT
                                                   pg.Id AS GroupId, pg.GroupName,
                                                   gv.AppraisedValue AS GroupAppraisedValue,
                                                   paa.ApproachType AS AppraisalMethod,
                                                   pfv.LandValue AS GroupLandValue,
                                                   pfv.BuildingCost AS GroupBuildingValue,
                                                   pfv.FinalValueAdjusted AS GroupUnitPrice,
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
                                                   mad.SerialNo            AS MachineSerialNo
                                               FROM appraisal.PropertyGroups pg
                                               LEFT JOIN appraisal.ValuationAnalyses va2 ON va2.AppraisalId = pg.AppraisalId
                                               LEFT JOIN appraisal.GroupValuations gv ON gv.PropertyGroupId = pg.Id AND gv.ValuationAnalysisId = va2.Id
                                               LEFT JOIN appraisal.PricingAnalysis pa ON pa.AnchorId = pg.Id AND pa.SubjectType = 0
                                               OUTER APPLY (
                                                   SELECT TOP 1 ApproachType
                                                   FROM appraisal.PricingAnalysisApproaches
                                                   WHERE PricingAnalysisId = pa.Id AND IsSelected = 1
                                                   ORDER BY Id
                                               ) paa
                                               OUTER APPLY (
                                                   SELECT TOP 1 fv.LandValue, fv.BuildingCost, fv.FinalValueAdjusted
                                                   FROM appraisal.PricingAnalysisApproaches pap
                                                   JOIN appraisal.PricingAnalysisMethods pm ON pm.ApproachId = pap.Id AND pm.IsSelected = 1
                                                   JOIN appraisal.PricingFinalValues fv ON fv.PricingMethodId = pm.Id
                                                   WHERE pap.PricingAnalysisId = pa.Id AND pap.IsSelected = 1
                                                   ORDER BY pm.Id
                                               ) pfv
                                               JOIN appraisal.PropertyGroupItems pgi ON pgi.PropertyGroupId = pg.Id
                                               JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
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
                                               WHERE pg.AppraisalId = @AppraisalId
                                               ORDER BY pg.GroupNumber, pgi.SequenceInGroup
                                               """;

    public const string Documents = """
                                    SELECT rd.DocumentType, rd.FilePath AS DocumentPath
                                    FROM request.RequestDocuments rd
                                    WHERE rd.RequestId = @RequestId AND rd.FilePath IS NOT NULL
                                    """;
}

internal sealed record AppraisalRow(
    Guid Id,
    string AppraisalNumber,
    string? Purpose,
    DateTime? CompletedAt,
    Guid RequestId);

internal sealed record AssignmentRow(
    Guid AssignmentId,
    string? AssignmentType,
    string? AssigneeUserId,
    string? AssigneeCompanyId,
    string? CompanyName,
    string? CompanyCode,
    string? UserFirstName,
    string? UserLastName,
    DateTime? AppointmentDateTime);

internal sealed record ValuationRow(
    decimal? AppraisedValue,
    decimal? ForcedSaleValue,
    decimal? InsuranceValue,
    decimal? MarketValue,
    DateTime? ValuationDate);

internal sealed record CollateralRow(
    Guid GroupId,
    string? GroupName,
    decimal? GroupAppraisedValue,
    string? AppraisalMethod,
    decimal? GroupLandValue,
    decimal? GroupBuildingValue,
    decimal? GroupUnitPrice,
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
    string? MachineSerialNo);

internal sealed record DocumentRow(string? DocumentType, string? DocumentPath);

internal static class AppraisalResultBuilder
{
    public static async Task<GetAppraisalResultResponse> BuildAsync(
        IDbConnection conn,
        AppraisalRow appraisal,
        CancellationToken cancellationToken)
    {
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

        var groupParams = new DynamicParameters();
        groupParams.Add("AppraisalId", appraisal.Id);
        var collateralRows = await conn.QueryAsync<CollateralRow>(
            new CommandDefinition(GetAppraisalResultSql.GroupsAndCollaterals, groupParams,
                cancellationToken: cancellationToken));

        var groups = collateralRows
            .GroupBy(r => r.GroupId)
            .Select(g =>
            {
                var first = g.First();
                var collaterals = g.Select(r => new AppraisalResultCollateral(
                    r.PropertyType,
                    r.TitleNo,
                    r.LandNo,
                    r.Rawang,
                    r.SurveyNo,
                    r.BookNo,
                    r.PageNo,
                    r.Rai,
                    r.Ngan,
                    r.Wa,
                    r.HouseNo,
                    r.BuildingType,
                    r.BuildingAge ?? r.CondoBuildingAge,
                    r.TotalFloor ?? r.CondoTotalFloor,
                    r.RoomNo,
                    r.FloorNo,
                    r.BuildingNo,
                    r.AreaUtilize,
                    r.ContractNo,
                    r.LesseeName,
                    r.LessorName,
                    r.Province ?? r.CadProvince,
                    r.District ?? r.CadDistrict,
                    r.SubDistrict ?? r.CadSubDistrict,
                    r.LandOffice ?? r.CadLandOffice,
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
                    first.AppraisalMethod,
                    first.GroupLandValue,
                    first.GroupBuildingValue,
                    first.GroupUnitPrice,
                    collaterals);
            })
            .ToList();

        var docParams = new DynamicParameters();
        docParams.Add("RequestId", appraisal.RequestId);
        var docRows = await conn.QueryAsync<DocumentRow>(
            new CommandDefinition(GetAppraisalResultSql.Documents, docParams, cancellationToken: cancellationToken));

        var documents = docRows
            .Select(d => new AppraisalResultDocument(d.DocumentType, d.DocumentPath))
            .ToList();

        string? valuerName = null;
        string? valuerCode = null;
        string? appraisalSource = null;
        if (assignment is not null)
        {
            if (string.Equals(assignment.AssignmentType, AssignmentType.External.Code, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(assignment.CompanyName))
                {
                    valuerName = assignment.CompanyName;
                    // External valuer code = company host code; internal (I) code is ignored for now.
                    valuerCode = assignment.CompanyCode;
                    appraisalSource = "E";
                }
            }
            else if (string.Equals(assignment.AssignmentType, AssignmentType.Internal.Code, StringComparison.OrdinalIgnoreCase))
            {
                var fullName = $"{assignment.UserFirstName} {assignment.UserLastName}".Trim();
                valuerName = string.IsNullOrWhiteSpace(fullName) ? null : fullName;
                appraisalSource = "I";
            }
        }

        return new GetAppraisalResultResponse(
            AppraisalNumber: appraisal.AppraisalNumber,
            AppraisalPurpose: appraisal.Purpose,
            AppraisalFee: fee,
            AppraisalSource: appraisalSource,
            ValuerName: valuerName,
            ValuerCode: valuerCode,
            ValuationDate: (valuation?.ValuationDate ?? appraisal.CompletedAt)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            AppraisalDate: assignment?.AppointmentDateTime?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TotalAppraisalValue: valuation?.AppraisedValue,
            ForceSalePrice: valuation?.ForcedSaleValue,
            FireInsurance: valuation?.InsuranceValue,
            MarketValue: valuation?.MarketValue,
            Groups: groups,
            Documents: documents);
    }
}

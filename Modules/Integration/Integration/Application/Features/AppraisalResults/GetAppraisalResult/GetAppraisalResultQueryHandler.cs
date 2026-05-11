using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

public class GetAppraisalResultQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalResultQuery, GetAppraisalResultResponse?>
{
    private const string SqlByAppraisalNumber = """
        SELECT a.Id, a.AppraisalNumber, a.Purpose, a.Channel, a.CompletedAt, a.RequestId
        FROM appraisal.Appraisals a
        WHERE a.AppraisalNumber = @AppraisalNumber AND a.IsDeleted = 0
        """;

    private const string SqlByExternalCaseKey = """
        SELECT a.Id, a.AppraisalNumber, a.Purpose, a.Channel, a.CompletedAt, a.RequestId
        FROM appraisal.Appraisals a
        JOIN request.Requests r ON r.Id = a.RequestId
        WHERE r.ExternalCaseKey = @ExternalCaseKey AND a.IsDeleted = 0
        """;

    private const string SqlActiveAssignment = """
        SELECT TOP 1 aa.Id AS AssignmentId, aa.AssigneeCompanyId,
               c.Id AS CompanyId, c.Name AS CompanyName
        FROM appraisal.AppraisalAssignments aa
        LEFT JOIN auth.Companies c ON c.Id = TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier)
        WHERE aa.AppraisalId = @AppraisalId
          AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
        ORDER BY aa.AssignedAt DESC
        """;

    private const string SqlFee = """
        SELECT TOP 1 af.TotalFeeAfterVAT
        FROM appraisal.AppraisalFees af
        WHERE af.AssignmentId = @AssignmentId
        """;

    private const string SqlValuationTotals = """
        SELECT va.AppraisedValue, va.ForcedSaleValue, va.InsuranceValue, va.ValuationDate
        FROM appraisal.ValuationAnalyses va
        WHERE va.AppraisalId = @AppraisalId
        """;

    private const string SqlGroupsAndCollaterals = """
        SELECT
            pg.Id AS GroupId, pg.GroupName,
            gv.AppraisedValue AS GroupAppraisedValue,
            paa.ApproachType AS AppraisalMethod,
            ap.Id AS PropertyId, ap.PropertyType,
            -- Land/LB fields (from LandAppraisalDetails + first LandTitle)
            lad.Province, lad.District, lad.SubDistrict,
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
            -- Lease fields
            leasd.ContractNo, leasd.LesseeName, leasd.LessorName,
            -- Vehicle identity fields
            vad.RegistrationNo      AS VehicleRegistrationNo,
            vad.Brand               AS VehicleBrand,
            vad.Model               AS VehicleModel,
            -- Vessel identity fields
            vsad.RegistrationNo     AS VesselRegistrationNo,
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
        LEFT JOIN appraisal.PricingAnalysis pa ON pa.PropertyGroupId = pg.Id
        OUTER APPLY (
            SELECT TOP 1 ApproachType
            FROM appraisal.PricingAnalysisApproaches
            WHERE PricingAnalysisId = pa.Id AND IsSelected = 1
            ORDER BY Id
        ) paa
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

    private const string SqlDocuments = """
        SELECT rd.DocumentType, rd.FilePath AS DocumentPath
        FROM request.RequestDocuments rd
        WHERE rd.RequestId = @RequestId AND rd.FilePath IS NOT NULL
        """;

    public async Task<GetAppraisalResultResponse?> Handle(
        GetAppraisalResultQuery query,
        CancellationToken cancellationToken)
    {
        var conn = connectionFactory.GetOpenConnection();

        // Step 1: Resolve appraisal
        AppraisalRow? appraisal;

        if (!string.IsNullOrWhiteSpace(query.AppraisalNumber))
        {
            var p = new DynamicParameters();
            p.Add("AppraisalNumber", query.AppraisalNumber);
            appraisal = await conn.QuerySingleOrDefaultAsync<AppraisalRow>(
                new CommandDefinition(SqlByAppraisalNumber, p, cancellationToken: cancellationToken));
        }
        else
        {
            var p = new DynamicParameters();
            p.Add("ExternalCaseKey", query.ExternalCaseKey);
            appraisal = await conn.QuerySingleOrDefaultAsync<AppraisalRow>(
                new CommandDefinition(SqlByExternalCaseKey, p, cancellationToken: cancellationToken));
        }

        if (appraisal is null)
        {
            return null;
        }

        // Step 2: Active assignment + valuer
        var assignmentParams = new DynamicParameters();
        assignmentParams.Add("AppraisalId", appraisal.Id);
        var assignment = await conn.QueryFirstOrDefaultAsync<AssignmentRow>(
            new CommandDefinition(SqlActiveAssignment, assignmentParams, cancellationToken: cancellationToken));

        // Step 3: Fee
        decimal? fee = null;
        if (assignment is not null)
        {
            var feeParams = new DynamicParameters();
            feeParams.Add("AssignmentId", assignment.AssignmentId);
            fee = await conn.QueryFirstOrDefaultAsync<decimal?>(
                new CommandDefinition(SqlFee, feeParams, cancellationToken: cancellationToken));
        }

        // Step 4: Valuation totals
        var valParams = new DynamicParameters();
        valParams.Add("AppraisalId", appraisal.Id);
        var valuation = await conn.QueryFirstOrDefaultAsync<ValuationRow>(
            new CommandDefinition(SqlValuationTotals, valParams, cancellationToken: cancellationToken));

        // Step 5: Groups + collaterals
        var groupParams = new DynamicParameters();
        groupParams.Add("AppraisalId", appraisal.Id);
        var collateralRows = await conn.QueryAsync<CollateralRow>(
            new CommandDefinition(SqlGroupsAndCollaterals, groupParams, cancellationToken: cancellationToken));

        var groups = collateralRows
            .GroupBy(r => r.GroupId)
            .Select(g =>
            {
                var first = g.First();
                var collaterals = g.Select(r => new AppraisalResultCollateral(
                    CollateralType: r.PropertyType,
                    TitleNo: r.TitleNo,
                    LandNo: r.LandNo,
                    Rawang: r.Rawang,
                    SurveyNo: r.SurveyNo,
                    BookNo: r.BookNo,
                    PageNo: r.PageNo,
                    Rai: r.Rai,
                    Ngan: r.Ngan,
                    Wa: r.Wa,
                    HouseNo: r.HouseNo,
                    BuildingType: r.BuildingType,
                    BuildingAge: r.BuildingAge ?? r.CondoBuildingAge,
                    TotalFloor: r.TotalFloor ?? r.CondoTotalFloor,
                    RoomNo: r.RoomNo,
                    FloorNo: r.FloorNo,
                    BuildingNo: r.BuildingNo,
                    AreaUtilize: r.AreaUtilize,
                    ContractNo: r.ContractNo,
                    LesseeName: r.LesseeName,
                    LessorName: r.LessorName,
                    Province: r.Province ?? r.CadProvince,
                    District: r.District ?? r.CadDistrict,
                    SubDistrict: r.SubDistrict ?? r.CadSubDistrict,
                    VehicleRegistrationNo: r.VehicleRegistrationNo,
                    VehicleBrand: r.VehicleBrand,
                    VehicleModel: r.VehicleModel,
                    VesselRegistrationNo: r.VesselRegistrationNo,
                    VesselName: r.VesselName,
                    VesselType: r.VesselType,
                    MachineName: r.MachineName,
                    MachineBrand: r.MachineBrand,
                    MachineModel: r.MachineModel,
                    MachineSerialNo: r.MachineSerialNo
                )).ToList();

                return new AppraisalResultGroup(
                    AppraisalValue: first.GroupAppraisedValue,
                    AppraisalMethod: first.AppraisalMethod,
                    Collaterals: collaterals);
            })
            .ToList();

        // Step 6: Documents
        var docParams = new DynamicParameters();
        docParams.Add("RequestId", appraisal.RequestId);
        var docRows = await conn.QueryAsync<DocumentRow>(
            new CommandDefinition(SqlDocuments, docParams, cancellationToken: cancellationToken));

        var documents = docRows
            .Select(d => new AppraisalResultDocument(d.DocumentType, d.DocumentPath))
            .ToList();

        return new GetAppraisalResultResponse(
            AppraisalNumber: appraisal.AppraisalNumber,
            AppraisalPurpose: appraisal.Purpose,
            AppraisalFee: fee,
            AppraisalSource: appraisal.Channel,
            AssignedCompanyId: assignment?.AssigneeCompanyId,
            AssignedCompanyName: assignment?.CompanyName,
            ValuationDate: valuation?.ValuationDate ?? appraisal.CompletedAt,
            TotalAppraisalValue: valuation?.AppraisedValue,
            ForceSalePrice: valuation?.ForcedSaleValue,
            FireInsurance: valuation?.InsuranceValue,
            Groups: groups,
            Documents: documents);
    }

    private sealed record AppraisalRow(Guid Id, string AppraisalNumber, string? Purpose, string? Channel, DateTime? CompletedAt, Guid RequestId);
    private sealed record AssignmentRow(Guid AssignmentId, string? AssigneeCompanyId, Guid? CompanyId, string? CompanyName);
    private sealed record ValuationRow(decimal? AppraisedValue, decimal? ForcedSaleValue, decimal? InsuranceValue, DateTime? ValuationDate);

    private sealed record CollateralRow(
        Guid GroupId,
        string? GroupName,
        decimal? GroupAppraisedValue,
        string? AppraisalMethod,
        Guid PropertyId,
        string? PropertyType,
        // Land
        string? Province,
        string? District,
        string? SubDistrict,
        string? TitleNo,
        string? LandNo,
        string? Rawang,
        string? SurveyNo,
        string? BookNo,
        string? PageNo,
        decimal? Rai,
        decimal? Ngan,
        decimal? Wa,
        // Building
        string? HouseNo,
        string? BuildingType,
        int? BuildingAge,
        decimal? TotalFloor,
        // Condo
        string? RoomNo,
        string? FloorNo,
        string? BuildingNo,
        int? CondoBuildingAge,
        decimal? CondoTotalFloor,
        decimal? AreaUtilize,
        string? CadProvince,
        string? CadDistrict,
        string? CadSubDistrict,
        // Lease
        string? ContractNo,
        string? LesseeName,
        string? LessorName,
        // Vehicle
        string? VehicleRegistrationNo,
        string? VehicleBrand,
        string? VehicleModel,
        // Vessel
        string? VesselRegistrationNo,
        string? VesselName,
        string? VesselType,
        // Machinery
        string? MachineName,
        string? MachineBrand,
        string? MachineModel,
        string? MachineSerialNo);

    private sealed record DocumentRow(string? DocumentType, string? DocumentPath);
}

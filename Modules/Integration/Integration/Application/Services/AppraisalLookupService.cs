using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;

namespace Integration.Application.Services;

public class AppraisalLookupService(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalLookupService> logger) : IAppraisalLookupService
{
    private const string SqlByAppraisalId = """
        SELECT
            a.AppraisalNumber AS AppraisalNumber,
            r.ExternalCaseKey AS ExternalCaseKey,
            r.ExternalSystem AS ExternalSystem
        FROM appraisal.Appraisals a
        JOIN request.Requests r ON r.Id = a.RequestId
        WHERE a.Id = @AppraisalId
        """;

    private const string SqlByRequestId = """
        SELECT TOP 1
            a.AppraisalNumber AS AppraisalNumber,
            r.ExternalCaseKey AS ExternalCaseKey,
            r.ExternalSystem AS ExternalSystem
        FROM appraisal.Appraisals a
        JOIN request.Requests r ON r.Id = a.RequestId
        WHERE r.Id = @RequestId
        ORDER BY a.CreatedOn
        """;

    public async Task<AppraisalKeys?> GetKeysAsync(Guid appraisalId, CancellationToken ct = default)
    {
        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", appraisalId);

        var command = new CommandDefinition(SqlByAppraisalId, parameters, cancellationToken: ct);
        var row = await connection.QuerySingleOrDefaultAsync<AppraisalKeysRow>(command);

        if (row is null)
        {
            logger.LogWarning("AppraisalLookupService: no appraisal found for AppraisalId {AppraisalId}", appraisalId);
            return null;
        }

        return new AppraisalKeys(row.AppraisalNumber, row.ExternalCaseKey, row.ExternalSystem);
    }

    private const string SqlPriorAppraisalByNumber = """
        SELECT TOP 1 a.Id, a.Status
        FROM appraisal.Appraisals a
        WHERE a.AppraisalNumber = @AppraisalNumber AND a.IsDeleted = 0
        """;

    public async Task<PriorAppraisalRef?> ResolvePriorAppraisalByNumberAsync(
        string appraisalNumber, CancellationToken ct = default)
    {
        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalNumber", appraisalNumber);

        var command = new CommandDefinition(SqlPriorAppraisalByNumber, parameters, cancellationToken: ct);
        var row = await connection.QueryFirstOrDefaultAsync<PriorAppraisalRefRow>(command);

        if (row is null)
        {
            logger.LogWarning(
                "AppraisalLookupService: no appraisal found for AppraisalNumber {AppraisalNumber}", appraisalNumber);
            return null;
        }

        return new PriorAppraisalRef(row.Id, row.Status);
    }

    private sealed record PriorAppraisalRefRow(Guid Id, string? Status);

    public async Task<AppraisalKeys?> GetKeysByRequestIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("RequestId", requestId);

        var command = new CommandDefinition(SqlByRequestId, parameters, cancellationToken: ct);
        var row = await connection.QueryFirstOrDefaultAsync<AppraisalKeysRow>(command);

        if (row is null)
        {
            logger.LogWarning("AppraisalLookupService: no appraisal found for RequestId {RequestId}", requestId);
            return null;
        }

        return new AppraisalKeys(row.AppraisalNumber, row.ExternalCaseKey, row.ExternalSystem);
    }

    // Column order MUST mirror PmaHeaderRow / PmaLandTitleData / PmaCondoData constructor order
    // exactly — Dapper binds positional records by position, not just by name (see remarks on
    // IAppraisalLookupService.PmaLandTitleData / PmaCondoData).
    private const string SqlPmaUpdateData = """
        SELECT
            a.AppraisalNumber AS AppraisalNumber,
            r.ExternalSystem AS ExternalSystem,
            rd.LoanApplicationNumber AS LoanApplicationNo,
            p.PropertyType AS PropertyType,
            p.SellingPrice AS SellingPrice,
            p.ForcedSalePrice AS ForcedSalePrice,
            p.BuildingInsurancePrice AS BuildingInsurancePrice
        FROM appraisal.AppraisalProperties p
        JOIN appraisal.Appraisals a ON a.Id = p.AppraisalId
        JOIN request.Requests r ON r.Id = a.RequestId
        LEFT JOIN request.RequestDetails rd ON rd.RequestId = a.RequestId
        WHERE p.Id = @PropertyId AND p.AppraisalId = @AppraisalId;

        SELECT
            lt.TitleNumber, lt.BookNumber, lt.PageNumber, lt.LandParcelNumber, lt.SurveyNumber, lt.Rawang,
            lt.AreaRai AS Rai, lt.AreaNgan AS Ngan, lt.AreaSquareWa AS SquareWa,
            lad.SubDistrict, lad.District, lad.Province
        FROM appraisal.LandAppraisalDetails lad
        JOIN appraisal.LandTitles lt ON lt.LandAppraisalDetailId = lad.Id
        WHERE lad.AppraisalPropertyId = @PropertyId
        ORDER BY lt.Id;  -- deterministic "first" title; matches the GET's Titles[0] (clustered PK / NEWSEQUENTIALID order)

        SELECT
            BuiltOnTitleNumber, CondoRegistrationNumber, RoomNumber, FloorNumber, BuildingNumber, CondoName,
            SubDistrict, District, Province
        FROM appraisal.CondoAppraisalDetails
        WHERE AppraisalPropertyId = @PropertyId;
        """;

    public async Task<PmaUpdateData?> GetPmaUpdateDataAsync(Guid appraisalId, Guid propertyId, CancellationToken ct = default)
    {
        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", appraisalId);
        parameters.Add("PropertyId", propertyId);

        var command = new CommandDefinition(SqlPmaUpdateData, parameters, cancellationToken: ct);
        await using var multi = await connection.QueryMultipleAsync(command);

        var header = await multi.ReadSingleOrDefaultAsync<PmaHeaderRow>();
        if (header is null)
        {
            logger.LogWarning(
                "AppraisalLookupService: no property found for AppraisalId {AppraisalId} PropertyId {PropertyId}",
                appraisalId, propertyId);
            return null;
        }

        var titles = (await multi.ReadAsync<PmaLandTitleData>()).ToList();
        var condo = await multi.ReadSingleOrDefaultAsync<PmaCondoData>();

        return new PmaUpdateData(
            header.AppraisalNumber,
            header.ExternalSystem,
            header.LoanApplicationNo,
            header.PropertyType,
            header.SellingPrice,
            header.ForcedSalePrice,
            header.BuildingInsurancePrice,
            titles,
            condo);
    }

    private sealed record AppraisalKeysRow(string? AppraisalNumber, string? ExternalCaseKey, string? ExternalSystem);

    // Column order MUST mirror the header SELECT in SqlPmaUpdateData exactly (Dapper positional
    // record binding).
    private sealed record PmaHeaderRow(
        string? AppraisalNumber,
        string? ExternalSystem,
        string? LoanApplicationNo,
        string? PropertyType,
        decimal? SellingPrice,
        decimal? ForcedSalePrice,
        decimal? BuildingInsurancePrice);
}

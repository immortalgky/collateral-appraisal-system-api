using Appraisal.Domain.Appraisals.Specifications;
using Dapper;

namespace Appraisal.Application.Services;

/// <inheritdoc />
public sealed class PricingAnalysisReadinessService(
    ISqlConnectionFactory sqlConnectionFactory,
    PricingAnalysisReadinessChecker checker)
    : IPricingAnalysisReadinessService
{
    // Single round trip: header + per-property booleans.
    // Property-level rules consume cheap EXISTS subqueries against the detail tables
    // rather than navigating the EF aggregate, so this stays fast and stays infrastructure-pure.
    private const string SnapshotSql = """
        SELECT  PG.AppraisalId,
                PG.Id AS GroupId,
                (SELECT COUNT(*) FROM appraisal.AppraisalComparables AC
                  WHERE AC.AppraisalId = PG.AppraisalId) AS MarketSurveyCount
        FROM appraisal.PropertyGroups PG
        WHERE PG.Id = @GroupId;

        SELECT  AP.Id AS PropertyId,
                AP.PropertyType,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM appraisal.BuildingAppraisalDetails B
                                        WHERE B.AppraisalPropertyId = AP.Id)
                          THEN 1 ELSE 0 END AS bit) AS HasBuildingDetail,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM appraisal.RentalInfos RI
                                        WHERE RI.AppraisalPropertyId = AP.Id)
                          THEN 1 ELSE 0 END AS bit) AS HasRentalInfo,
                CAST(CASE WHEN EXISTS (SELECT 1
                                         FROM appraisal.RentalInfos RI
                                         INNER JOIN appraisal.RentalScheduleEntries RSE
                                                 ON RSE.RentalInfoId = RI.Id
                                        WHERE RI.AppraisalPropertyId = AP.Id)
                          THEN 1 ELSE 0 END AS bit) AS HasRentalSchedule,
                ISNULL(AP.[Status], 'Draft') AS [Status]
        FROM appraisal.PropertyGroupItems PGI
        INNER JOIN appraisal.AppraisalProperties AP
                ON AP.Id = PGI.AppraisalPropertyId
        WHERE PGI.PropertyGroupId = @GroupId
        ORDER BY PGI.SequenceInGroup;
    """;

    public async Task<ReadinessSnapshot?> GetSnapshotByGroupIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var connection = sqlConnectionFactory.GetOpenConnection();

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(SnapshotSql, new { GroupId = groupId }, cancellationToken: cancellationToken));

        var header = await multi.ReadFirstOrDefaultAsync<HeaderRow>();
        if (header is null)
            return null;

        var properties = (await multi.ReadAsync<PropertyRow>())
            .Select(p => new PropertySnapshot(
                p.PropertyId,
                p.PropertyType,
                p.HasBuildingDetail,
                p.HasRentalInfo,
                p.HasRentalSchedule,
                p.Status))
            .ToList();

        return new ReadinessSnapshot(
            header.AppraisalId,
            header.GroupId,
            header.MarketSurveyCount,
            properties);
    }

    public async Task<ReadinessResult?> EvaluateByGroupIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotByGroupIdAsync(groupId, cancellationToken);
        return snapshot is null ? null : checker.Evaluate(snapshot);
    }

    private sealed record HeaderRow(Guid AppraisalId, Guid GroupId, int MarketSurveyCount);

    private sealed record PropertyRow(
        Guid PropertyId,
        string PropertyType,
        bool HasBuildingDetail,
        bool HasRentalInfo,
        bool HasRentalSchedule,
        string Status);
}

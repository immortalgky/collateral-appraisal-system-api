using Dapper;

namespace Appraisal.Application.Features.Appraisals.GetMachinerySummarySuggestedCounts;

/// <summary>
/// Derives the six Section 3.1 head-counts from the machines recorded on the appraisal.
/// </summary>
public class GetMachinerySummarySuggestedCountsQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory
) : IQueryHandler<GetMachinerySummarySuggestedCountsQuery, GetMachinerySummarySuggestedCountsResult>
{
    /// <summary>MachineStatus parameter code for "installed".</summary>
    private const string InstalledStatus = "1";

    /// <summary>MachineStatus parameter code for "under procurement".</summary>
    private const string UnderProcurementStatus = "2";

    /// <summary>ConditionUse parameter code for "in used".</summary>
    private const string InUsedCondition = "01";

    /// <summary>ConditionUse parameter code for "not in used" — the scrap case.</summary>
    private const string NotInUsedCondition = "02";

    /// <summary>ConditionUse parameter code for "not found" — surveyed but missing on site.</summary>
    private const string NotFoundCondition = "03";

    public async Task<GetMachinerySummarySuggestedCountsResult> Handle(
        GetMachinerySummarySuggestedCountsQuery query,
        CancellationToken cancellationToken)
    {
        // COUNT (not SUM) so an appraisal with no machines yields zeroes rather than a row of NULLs.
        // A machine with no ConditionUse recorded counts as surveyed: only an explicit "not found"
        // takes it out.
        const string sql = """
                           SELECT
                               COUNT(CASE WHEN mad.ConditionUse IS NULL OR mad.ConditionUse <> @NotFound THEN 1 END) AS SurveyedNumber,
                               COUNT(CASE WHEN mad.IsPriceCertified = 1 THEN 1 END) AS AppraisalNumber,
                               COUNT(CASE WHEN mad.InstallationStatus = @Installed
                                           AND mad.ConditionUse = @InUsed THEN 1 END) AS InstalledAndUseCount,
                               COUNT(CASE WHEN mad.ConditionUse = @NotInUsed
                                           AND mad.IsPriceCertified = 1 THEN 1 END) AS AppraisalScrapCount,
                               COUNT(CASE WHEN mad.InstallationStatus = @UnderProcurement THEN 1 END) AS AppraisedByDocumentCount,
                               COUNT(CASE WHEN mad.InstallationStatus = @UnderProcurement THEN 1 END) AS NotInstalledCount
                           FROM appraisal.AppraisalProperties ap
                           JOIN appraisal.MachineryAppraisalDetails mad ON mad.AppraisalPropertyId = ap.Id
                           WHERE ap.AppraisalId = @AppraisalId AND ap.PropertyType = 'MAC';
                           """;

        var connection = sqlConnectionFactory.GetOpenConnection();

        return await connection.QuerySingleAsync<GetMachinerySummarySuggestedCountsResult>(
            new CommandDefinition(
                sql,
                new
                {
                    query.AppraisalId,
                    Installed = InstalledStatus,
                    UnderProcurement = UnderProcurementStatus,
                    InUsed = InUsedCondition,
                    NotInUsed = NotInUsedCondition,
                    NotFound = NotFoundCondition
                },
                cancellationToken: cancellationToken));
    }
}

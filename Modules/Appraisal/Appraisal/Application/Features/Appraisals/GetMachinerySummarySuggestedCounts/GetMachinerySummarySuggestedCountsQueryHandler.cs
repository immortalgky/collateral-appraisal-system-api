using Dapper;

namespace Appraisal.Application.Features.Appraisals.GetMachinerySummarySuggestedCounts;

/// <summary>
/// Derives the six Section 3.1 head-counts from the machines recorded on the appraisal, both in
/// total and per property group.
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

    /// <summary>Shape of the totals statement: the six counts over the machines themselves.</summary>
    private sealed record MachineryCountSet(
        int SurveyedNumber,
        int AppraisalNumber,
        int InstalledAndUseCount,
        int AppraisalScrapCount,
        int AppraisedByDocumentCount,
        int NotInstalledCount);

    public async Task<GetMachinerySummarySuggestedCountsResult> Handle(
        GetMachinerySummarySuggestedCountsQuery query,
        CancellationToken cancellationToken)
    {
        // Two statements, one round trip. The totals are counted over the machines themselves, NOT
        // summed from the group rows: PropertyGroupItems is unique on (PropertyGroupId,
        // AppraisalPropertyId) only, so the same machine may sit in two groups, and summing the
        // parts would then count it twice in a figure that is meant to say how many machines there
        // are. The split still shows where each total came from; when a machine is shared, the
        // parts add up to more than the total, which is the truth about the grouping.
        //
        // COUNT (not SUM) so a group with no matching machines yields zero rather than NULL, and
        // LEFT JOIN so a machine that has not been put in a group yet still appears; it comes back
        // under a null GroupId for the caller to label.
        //
        // A machine with no ConditionUse recorded counts as surveyed: only an explicit "not found"
        // takes it out.
        //
        // ประเมินตามเอกสาร is under-procurement AND priced from a quotation, which is what the rule
        // shown next to the field says; without the invoice test it would be the same number as
        // ยังไม่ติดตั้ง, and one of the two columns would say nothing.
        const string sql = """
                           SELECT
                               pg.Id           AS GroupId,
                               pg.GroupNumber  AS GroupNumber,
                               pg.GroupName    AS GroupName,
                               COUNT(CASE WHEN mad.ConditionUse IS NULL OR mad.ConditionUse <> @NotFound THEN 1 END) AS SurveyedNumber,
                               COUNT(CASE WHEN mad.IsPriceCertified = 1 THEN 1 END) AS AppraisalNumber,
                               COUNT(CASE WHEN mad.InstallationStatus = @Installed
                                           AND mad.ConditionUse = @InUsed THEN 1 END) AS InstalledAndUseCount,
                               COUNT(CASE WHEN mad.ConditionUse = @NotInUsed
                                           AND mad.IsPriceCertified = 1 THEN 1 END) AS AppraisalScrapCount,
                               COUNT(CASE WHEN mad.InstallationStatus = @UnderProcurement
                                           AND mad.InvoiceNumber IS NOT NULL
                                           AND mad.InvoiceNumber <> '' THEN 1 END) AS AppraisedByDocumentCount,
                               COUNT(CASE WHEN mad.InstallationStatus = @UnderProcurement THEN 1 END) AS NotInstalledCount
                           FROM appraisal.AppraisalProperties ap
                           JOIN appraisal.MachineryAppraisalDetails mad ON mad.AppraisalPropertyId = ap.Id
                           LEFT JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id
                           LEFT JOIN appraisal.PropertyGroups pg ON pg.Id = pgi.PropertyGroupId
                           WHERE ap.AppraisalId = @AppraisalId AND ap.PropertyType = 'MAC'
                           GROUP BY pg.Id, pg.GroupNumber, pg.GroupName
                           ORDER BY pg.GroupNumber, pg.GroupName;

                           SELECT
                               COUNT(CASE WHEN mad.ConditionUse IS NULL OR mad.ConditionUse <> @NotFound THEN 1 END) AS SurveyedNumber,
                               COUNT(CASE WHEN mad.IsPriceCertified = 1 THEN 1 END) AS AppraisalNumber,
                               COUNT(CASE WHEN mad.InstallationStatus = @Installed
                                            AND mad.ConditionUse = @InUsed THEN 1 END) AS InstalledAndUseCount,
                               COUNT(CASE WHEN mad.ConditionUse = @NotInUsed
                                            AND mad.IsPriceCertified = 1 THEN 1 END) AS AppraisalScrapCount,
                               COUNT(CASE WHEN mad.InstallationStatus = @UnderProcurement
                                            AND mad.InvoiceNumber IS NOT NULL
                                            AND mad.InvoiceNumber <> '' THEN 1 END) AS AppraisedByDocumentCount,
                               COUNT(CASE WHEN mad.InstallationStatus = @UnderProcurement THEN 1 END) AS NotInstalledCount
                           FROM appraisal.AppraisalProperties ap
                           JOIN appraisal.MachineryAppraisalDetails mad ON mad.AppraisalPropertyId = ap.Id
                           WHERE ap.AppraisalId = @AppraisalId AND ap.PropertyType = 'MAC';
                           """;

        var connection = sqlConnectionFactory.GetOpenConnection();

        await using var reader = await connection.QueryMultipleAsync(
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

        var groups = (await reader.ReadAsync<MachinerySuggestedCountsByGroup>()).ToList();
        var totals = await reader.ReadSingleAsync<MachineryCountSet>();

        return new GetMachinerySummarySuggestedCountsResult(
            SurveyedNumber: totals.SurveyedNumber,
            AppraisalNumber: totals.AppraisalNumber,
            InstalledAndUseCount: totals.InstalledAndUseCount,
            AppraisalScrapCount: totals.AppraisalScrapCount,
            AppraisedByDocumentCount: totals.AppraisedByDocumentCount,
            NotInstalledCount: totals.NotInstalledCount,
            Groups: groups);
    }
}

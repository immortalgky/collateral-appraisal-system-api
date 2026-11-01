using Dapper;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

public class AssignmentEmailCaseReader(ISqlConnectionFactory connectionFactory) : IAssignmentEmailCaseReader
{
    public async Task<AssignmentEmailCase?> GetCaseAsync(Guid appraisalId, CancellationToken ct)
    {
        // Column order = AssignmentEmailCase's constructor order: Dapper maps positional records by position.
        // The current assignment is the newest live row, as AppraisalAssignmentElection picks it.
        // Fee payment text is the FeePaymentMethod label of the code the case's fee carries: the current
        // assignment's fee, else the newest live one, as the fee preview picks it (FeeShellFactory.LatestFee).
        const string sql = """
                           SELECT  a.AppraisalNumber,
                                   cu.Name                                           AS CustomerName,
                                   rd.ContactPersonName                              AS ContactName,
                                   rd.ContactPersonPhone                             AS ContactPhone,
                                   pm.Description                                    AS FeePaymentText,
                                   asg.AssignmentMethod,
                                   asg.AssigneeCompanyId,
                                   NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(ku.FirstName, N''), N' ', NULLIF(ku.LastName, N'')))), N'')
                                                                                     AS KeyerName,
                                   asg.InternalFollowupAssignmentMethod              AS FollowupMethod,
                                   NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(fu.FirstName, N''), N' ', NULLIF(fu.LastName, N'')))), N'')
                                                                                     AS FollowupName,
                                   asg.Remark,
                                   a.Status                                          AS AppraisalStatus,
                                   asg.Id                                            AS AssignmentId,
                                   asg.AssignmentType,
                                   asg.AssignmentStatus,
                                   asg.AssigneeUserId                                AS KeyerUserId,
                                   asg.InternalAppraiserId                           AS FollowupUserId
                           FROM appraisal.Appraisals a
                                    -- vw_AppraisalList's customer rule (first customer by Id), without the whole view.
                                    OUTER APPLY (SELECT TOP 1 rc.Name
                                                 FROM request.RequestCustomers rc
                                                 WHERE rc.RequestId = a.RequestId
                                                 ORDER BY rc.Id) cu
                                    LEFT JOIN request.RequestDetails rd ON rd.RequestId = a.RequestId
                                    OUTER APPLY (SELECT TOP 1 x.*
                                                 FROM appraisal.AppraisalAssignments x
                                                 WHERE x.AppraisalId = a.Id
                                                   AND x.AssignmentStatus NOT IN (N'Rejected', N'Cancelled')
                                                 ORDER BY x.CreatedAt DESC, x.Id DESC) asg
                                    OUTER APPLY (SELECT TOP 1 f.FeePaymentType
                                                 FROM appraisal.AppraisalFees f
                                                          JOIN appraisal.AppraisalAssignments fa ON fa.Id = f.AssignmentId
                                                 WHERE fa.AppraisalId = a.Id
                                                   AND fa.AssignmentStatus NOT IN (N'Rejected', N'Cancelled')
                                                 ORDER BY CASE WHEN f.AssignmentId = asg.Id THEN 0 ELSE 1 END,
                                                          f.CreatedAt DESC, f.Id DESC) fee
                                    OUTER APPLY (SELECT TOP 1 p.Description
                                                 FROM parameter.Parameters p
                                                 WHERE p.[Group] = N'FeePaymentMethod' AND p.Code = fee.FeePaymentType
                                                   AND p.[Language] = N'TH' AND p.IsActive = 1
                                                 ORDER BY p.ParId) pm
                                    LEFT JOIN auth.AspNetUsers ku ON ku.UserName = asg.AssigneeUserId
                                    LEFT JOIN auth.AspNetUsers fu ON fu.UserName = asg.InternalAppraiserId
                           WHERE a.Id = @AppraisalId AND a.IsDeleted = 0;
                           """;

        var connection = connectionFactory.GetOpenConnection();
        return await connection.QueryFirstOrDefaultAsync<AssignmentEmailCase>(
            new CommandDefinition(sql, new { AppraisalId = appraisalId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AssignmentEmailDocument>> GetDocumentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        const string sql = """
                           SELECT d.Id, d.FileName, d.FileSizeBytes
                           FROM document.Documents d
                           WHERE d.Id IN @Ids AND d.IsDeleted = 0 AND d.IsActive = 1
                           """;

        var connection = connectionFactory.GetOpenConnection();
        return (await connection.QueryAsync<AssignmentEmailDocument>(
            new CommandDefinition(sql, new { Ids = ids }, cancellationToken: ct))).ToList();
    }
}

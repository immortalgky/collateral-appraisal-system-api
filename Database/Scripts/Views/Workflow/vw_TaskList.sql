CREATE
OR ALTER
VIEW workflow.vw_TaskList
AS
SELECT pt.Id                                                                              AS Id,
       a.Id                                                                               AS AppraisalId,
       r.Id                                                                               AS RequestId,
       pt.WorkflowInstanceId,
       pt.ActivityId,
       a.AppraisalNumber,
       r.RequestNumber,
       c.Name                                                                             AS CustomerName,
       pt.TaskName                                                                        AS TaskType,
       pt.TaskDescription,
       r.Purpose,
       p.PropertyType                                                                     AS PropertyType,
       -- Derive appraisal status: CompletedAt trumps all; then workflow activity; fallback to request status
       CASE
           WHEN a.CompletedAt IS NOT NULL THEN 'Completed'
           WHEN pt.ActivityId IN ('appraisal-initiation-check', 'appraisal-assignment') THEN 'Pending'
           WHEN pt.ActivityId IN
                ('appraisal-book-verification', 'int-appraisal-check', 'int-appraisal-verification', 'pending-approval')
               THEN 'UnderReview'
           WHEN a.Id IS NOT NULL THEN 'InProgress'
           ELSE r.Status
           END                                                                            AS Status,
       pt.TaskStatus                                                                      AS PendingTaskStatus,
       ap.AppointmentDateTime,
       pt.AssignedTo                                                                      AS AssigneeUserId,
       pt.AssignedType                                                                    AS AssignedType,
       pt.AssigneeCompanyId                                                               AS AssigneeCompanyId,
       COALESCE(a.RequestedBy, r.Requestor)                                               AS RequestedBy,
       COALESCE(CONCAT(u.FirstName, ' ', u.LastName), ISNULL(a.RequestedBy, r.Requestor)) AS RequestedByName,
       COALESCE(a.RequestedAt, r.RequestedAt)                                             AS RequestReceivedDate,
       pt.AssignedAt                                                                      AS AssignedDate,
       pt.Movement,
       AA.InternalAppraiserId                                                             AS InternalFollowupStaff,
       CASE
           WHEN AA.AssignmentType = 'Internal' THEN AA.InternalAppraiserId
           WHEN AA.AssignmentType = 'External' THEN comp.Name
           END                                                                            AS Appraiser,
       COALESCE(a.Priority, r.Priority)                                                   AS Priority,
       pt.DueAt,
       pt.SlaStatus,
       DATEDIFF(HOUR, pt.AssignedAt, GETDATE())                                           AS ElapsedHours,
       CASE WHEN pt.DueAt IS NOT NULL THEN DATEDIFF(HOUR, GETDATE(), pt.DueAt) END        AS RemainingHours,
       pt.WorkingBy,
       pt.LockedAt,
       -- TaskId: the domain entity id that FE should route to.
       -- Quotation tasks use the QuotationRequestId; document-followup tasks use DocumentFollowup.Id;
       -- regular tasks have NULL (FE falls back to workflowInstanceId, preserving legacy behaviour).
       COALESCE(qr.Id, df.Id)                                                             AS TaskId,
       -- Quotation context (resolved via CorrelationId = QuotationRequestId for quotation-workflow tasks)
       qr.Id                                                                              AS QuotationRequestId,
       qr.QuotationNumber,
       qr.Status                                                                          AS QuotationStatus,
       qr.DueDate                                                                         AS QuotationDueDate,
       qrm.FirstName + ' ' + qrm.LastName                                                AS QuotationRmName
FROM workflow.PendingTasks pt
         -- Followup tasks (ProvideAdditionalDocuments) carry the DocumentFollowup.Id as
         -- CorrelationId, not the RequestId. Resolve the effective RequestId via the
         -- DocumentFollowups row whose FollowupWorkflowInstanceId matches pt.WorkflowInstanceId.
         -- For non-followup tasks, df.RequestId is NULL and COALESCE falls through to
         -- pt.CorrelationId (which already equals the requestId).
         OUTER APPLY (SELECT TOP 1 Id, RequestId, AppraisalId
                      FROM workflow.DocumentFollowups
                      WHERE FollowupWorkflowInstanceId = pt.WorkflowInstanceId) df
         -- Quotation context: pt.CorrelationId = QuotationRequestId for quotation-workflow tasks
         OUTER APPLY (SELECT TOP 1 qr2.Id, qr2.QuotationNumber, qr2.Status, qr2.DueDate, qr2.RmUserId
                      FROM appraisal.QuotationRequests qr2
                      WHERE qr2.Id = pt.CorrelationId) qr
         LEFT JOIN auth.AspNetUsers qrm ON qrm.Id = TRY_CAST(qr.RmUserId AS nvarchar(450))
         -- Resolve the effective RequestId:
         --   1. document-followup tasks carry df.RequestId
         --   2. quotation-workflow tasks carry qr.RequestId (via QuotationRequestAppraisals)
         --   3. regular appraisal-workflow tasks carry pt.CorrelationId = RequestId
         OUTER APPLY (SELECT TOP 1 qra2.AppraisalId AS QuotationAppraisalId
                      FROM appraisal.QuotationRequestAppraisals qra2
                      WHERE qra2.QuotationRequestId = qr.Id) qra_first
         OUTER APPLY (SELECT TOP 1 a2.Id, a2.RequestId
                      FROM appraisal.Appraisals a2
                      WHERE a2.Id = qra_first.QuotationAppraisalId) qra_appraisal
         LEFT JOIN appraisal.Appraisals a ON a.Id = COALESCE(
             qra_appraisal.Id,  -- quotation task: pick first appraisal from the quotation
             -- followup or regular task: join via RequestId
             (SELECT TOP 1 a3.Id FROM appraisal.Appraisals a3
              WHERE a3.RequestId = COALESCE(df.RequestId, pt.CorrelationId))
         )
         LEFT JOIN request.Requests r ON r.Id = COALESCE(
             qra_appraisal.RequestId,   -- quotation task: request from the linked appraisal
             df.RequestId,              -- followup task
             pt.CorrelationId           -- regular appraisal-workflow task
         )
         OUTER APPLY (SELECT TOP 1 Name
                          FROM request.RequestCustomers
                          WHERE RequestId = r.Id) C
        OUTER APPLY (SELECT STRING_AGG(PropertyType, ',') AS PropertyType
                      FROM request.RequestProperties
                      WHERE RequestId = r.Id
                      GROUP BY RequestId) P
        OUTER APPLY (SELECT TOP 1 Id, AssignmentType, InternalAppraiserId, AssigneeCompanyId
                      FROM appraisal.AppraisalAssignments
                      WHERE AppraisalId = a.Id
                        AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                      ORDER BY AssignedAt DESC) AA
        OUTER APPLY (SELECT TOP 1 AppointmentDateTime
                      FROM appraisal.Appointments
                      WHERE AssignmentId = AA.Id
                        AND Status != 'Cancelled') ap
        LEFT JOIN auth.Companies comp
ON comp.Id = TRY_CAST(AA.AssigneeCompanyId AS uniqueidentifier)
    LEFT JOIN auth.AspNetUsers u ON u.UserName = ISNULL(a.RequestedBy, r.Requestor)

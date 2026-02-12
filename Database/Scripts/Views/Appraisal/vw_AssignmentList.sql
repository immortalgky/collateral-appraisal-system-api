CREATE
OR ALTER
VIEW appraisal.vw_AssignmentList AS
SELECT a.Id,
       a.AppraisalId,
       a.AssignmentMode,
       a.AssignmentStatus,
       a.AssigneeUserId,
       a.AssigneeCompanyId,
       a.ExternalAppraiserName,
       a.AssignmentSource,
       a.ReassignmentNumber,
       a.ProgressPercent,
       a.AssignedAt,
       a.AssignedBy,
       a.StartedAt,
       a.CompletedAt,
       a.RejectionReason,
       a.CancellationReason,
       a.CreatedAt
FROM appraisal.AppraisalAssignments a

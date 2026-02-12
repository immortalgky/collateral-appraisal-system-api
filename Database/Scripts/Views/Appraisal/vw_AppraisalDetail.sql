CREATE
OR ALTER
VIEW appraisal.vw_AppraisalDetail AS
SELECT a.Id,
       a.AppraisalNumber,
       a.RequestId,
       a.Status,
       a.AppraisalType,
       a.Priority,
       a.SLADays,
       a.SLADueDate,
       a.SLAStatus,
       a.ActualDaysToComplete,
       a.IsWithinSLA,
       (SELECT COUNT(*) FROM appraisal.AppraisalProperties ap WHERE ap.AppraisalId = a.Id)  AS PropertyCount,
       (SELECT COUNT(*) FROM appraisal.PropertyGroups pg WHERE pg.AppraisalId = a.Id)       AS GroupCount,
       (SELECT COUNT(*) FROM appraisal.AppraisalAssignments aa WHERE aa.AppraisalId = a.Id) AS AssignmentCount,
       a.CreatedAt,
       a.CreatedBy,
       a.UpdatedAt,
       a.UpdatedBy
FROM appraisal.Appraisals a

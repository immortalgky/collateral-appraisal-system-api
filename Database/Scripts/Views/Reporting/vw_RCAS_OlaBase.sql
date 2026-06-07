-- Shared base view for the OLA reports (RCAS003/005/006/011). One row per appraisal with the
-- columns those reports display + RequestId (workflow correlation) and AppointmentDate, which the
-- C# OlaTimingService needs to compute the business-time OLA segments from workflow CompletedTasks.
CREATE
OR ALTER VIEW reporting.vw_RCAS_OlaBase
AS
SELECT v.Id,
       v.RequestId,
       v.CreatedAt              AS AppraisalCreateDate,
       v.AppraisalNumber,
       v.CustomerName,
       v.Purpose,
       v.FacilityLimit          AS ApplyLimitAmount,
       ct.CollateralType,
       v.Channel,
       v.AssignmentType,
       v.CompanyName            AS AppraisalCompany,
       v.AssigneeUserId         AS InternalAppraisalStaff,
       v.RequestedBy            AS RequestorCode,
       v.BankingSegment,
       v.AppointmentDateTime    AS AppointmentDate,
       v.AssignedDate           AS AssignDate,
       v.Status                 AS AppraisalStatus
FROM appraisal.vw_AppraisalList v
         OUTER APPLY (
    SELECT CollateralType = STRING_AGG(x.PropertyType, ', ')
    FROM (SELECT DISTINCT ap.PropertyType
          FROM appraisal.AppraisalProperties ap
          WHERE ap.AppraisalId = v.Id) x
    ) ct;

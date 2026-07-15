CREATE
OR ALTER
VIEW appraisal.vw_AppraisalEvaluationHeader AS
SELECT
    a.Id                                    AS AppraisalId,
    a.AppraisalNumber,
    a.Status                                AS AppraisalStatus,
    a.BankingSegment                        AS BankingSegment,

    -- Customer name from request
    c.Name                                  AS CustomerName,

    -- Report received date: appraiser handoff timestamp (first InProgress -> UnderReview)
    ext.SubmittedAt                         AS ReportReceivedDate,

    -- Current external assignment's appraiser
    comp.Name                               AS AppraiserCompanyName,
    ext.AssigneeCompanyId,

    NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, ' ', u.LastName))), '')
                                            AS InternalAppraiserName,
    ext.InternalAppraiserId,

    -- Distinct collateral types across the appraisal's properties, humanized
    STUFF((
        SELECT DISTINCT ', ' +
            CASE p.PropertyType
                WHEN 'L'   THEN 'Land'
                WHEN 'B'   THEN 'Building'
                WHEN 'LB'  THEN 'Land and building'
                WHEN 'U'   THEN 'Condominium'
                WHEN 'VEH' THEN 'Vehicle'
                WHEN 'VES' THEN 'Vessel'
                WHEN 'MAC' THEN 'Machinery'
                WHEN 'LSL' THEN 'Lease Agreement Land'
                WHEN 'LSB' THEN 'Lease Agreement Building'
                WHEN 'LS'  THEN 'Lease Agreement Land and building'
                WHEN 'LSU' THEN 'Lease Agreement Condo'
                ELSE p.PropertyType
            END
        FROM appraisal.AppraisalProperties p
        WHERE p.AppraisalId = a.Id
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 2, '')   AS CollateralTypes,

    -- Distinct inspection dates from appointments tied to this appraisal's assignments
    -- Date-only (style 23 = yyyy-mm-dd); time is not meaningful for this column.
    STUFF((
        SELECT DISTINCT ', ' + CONVERT(NVARCHAR(10), ap.AppointmentDateTime, 23)
        FROM appraisal.Appointments ap
            INNER JOIN appraisal.AppraisalAssignments aa3 ON aa3.Id = ap.AssignmentId
        WHERE aa3.AppraisalId = a.Id
          AND ap.Status <> 'Cancelled'
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 2, '')   AS InspectionDates,

    -- Bank-side department of appraisal. Data source not yet identified; surface NULL
    -- so the FE renders the placeholder.
    CAST(NULL AS NVARCHAR(200))             AS DepartmentOfAppraisal

FROM appraisal.Appraisals a

    -- Customer name
    OUTER APPLY (
        SELECT TOP 1 Name
        FROM request.RequestCustomers
        WHERE RequestId = a.RequestId
    ) c

    -- Most recent External assignment that is not Rejected / Cancelled
    LEFT JOIN (
        SELECT
            aa.AppraisalId,
            aa.AssigneeCompanyId,
            aa.InternalAppraiserId,
            aa.SubmittedAt,
            ROW_NUMBER() OVER (PARTITION BY aa.AppraisalId ORDER BY aa.AssignedAt DESC, aa.CreatedAt DESC, aa.Id DESC) AS rn
        FROM appraisal.AppraisalAssignments aa
        WHERE aa.AssignmentType = 'External'
          AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
    ) ext ON ext.AppraisalId = a.Id AND ext.rn = 1

    -- Appraiser company
    LEFT JOIN auth.Companies comp
        ON comp.Id = TRY_CAST(ext.AssigneeCompanyId AS uniqueidentifier)

    LEFT JOIN auth.AspNetUsers u
        ON u.NormalizedUserName = UPPER(ext.InternalAppraiserId)

WHERE a.IsDeleted = 0

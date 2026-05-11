CREATE
OR ALTER
VIEW appraisal.vw_AppraisalCopyTemplate AS
SELECT
    -- Appraisal snapshot (PrevAppraisal block)
    a.Id                    AS AppraisalId,
    a.AppraisalNumber,
    apt.AppointmentDateTime AS AppointmentDate,
    a.Status                AS Status,
    va.AppraisedValue       AS AppraisalValue,

    -- Link to source request
    a.RequestId,

    -- RequestDetail: Address
    d.HouseNumber,
    d.ProjectName,
    d.Moo,
    d.Soi,
    d.Road,
    d.SubDistrict,
    d.District,
    d.Province,
    d.Postcode,

    -- RequestDetail: Contact
    d.ContactPersonName,
    d.ContactPersonPhone,
    d.DealerCode,

    -- RequestDetail: LoanDetail
    d.BankingSegment,
    d.LoanApplicationNumber,
    d.FacilityLimit,
    d.AdditionalFacilityLimit,
    d.PreviousFacilityLimit,
    d.TotalSellingPrice,

    -- RequestDetail: misc
    d.HasAppraisalBook

FROM appraisal.Appraisals a
         JOIN request.Requests r ON r.Id = a.RequestId
         JOIN request.RequestDetails d ON d.RequestId = r.Id
         LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
         OUTER APPLY (SELECT TOP 1 ap.AppointmentDateTime
                      FROM appraisal.Appointments ap
                               JOIN appraisal.AppraisalAssignments aa ON aa.Id = ap.AssignmentId
                      WHERE aa.AppraisalId = a.Id
                        AND ap.Status != 'Cancelled'
                      ORDER BY ap.AppointmentDateTime DESC) apt
WHERE a.IsDeleted = 0
  AND r.IsDeleted = 0

CREATE
OR ALTER
VIEW request.vw_Requests AS
SELECT Id,
       RequestNumber,
       Status,
       Purpose,
       Channel,
       Requestor,
       RequestorName,
       RequestedAt,
       Creator,
       CreatorName,
       CreatedAt,
       CompletedAt,
       Priority,
       IsPma,
       HasAppraisalBook,
       BankingSegment,
       LoanApplicationNumber,
       FacilityLimit,
       AdditionalFacilityLimit,
       PreviousFacilityLimit,
       TotalSellingPrice,
       PrevAppraisalId,
       HouseNumber,
       ProjectName,
       Moo,
       Soi,
       Road,
       SubDistrict,
       District,
       Province,
       Postcode,
       ContactPersonName,
       ContactPersonPhone,
       DealerCode,
       AppointmentDate,
       AppointmentLocation,
       FeePaymentType,
       AbsorbedFee,
       FeeNotes
FROM request.Requests r
         JOIN request.RequestDetails d ON d.RequestId = r.Id
WHERE IsDeleted = 0



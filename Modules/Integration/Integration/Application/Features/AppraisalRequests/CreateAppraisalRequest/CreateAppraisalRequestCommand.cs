using Shared.CQRS;

namespace Integration.Application.Features.AppraisalRequests.CreateAppraisalRequest;

public record CreateAppraisalRequestCommand(
    string ExternalCaseKey,
    string Purpose,
    string Channel,
    string Priority,
    AppraisalRequestLoanDetail? LoanDetail,
    AppraisalRequestContact? Contact,
    List<AppraisalRequestCustomer>? Customers,
    List<AppraisalRequestProperty>? Properties,
    List<Guid>? DocumentIds
) : ICommand<CreateAppraisalRequestResult>;

public record AppraisalRequestLoanDetail(
    string? LoanApplicationNumber,
    string? BankingSegment,
    decimal? FacilityLimit
);

public record AppraisalRequestContact(
    string? ContactPersonName,
    string? ContactPersonPhone
);

public record AppraisalRequestCustomer(
    string Name,
    string? ContactNumber
);

public record AppraisalRequestProperty(
    string PropertyType,
    string? BuildingType,
    decimal? SellingPrice
);

public record CreateAppraisalRequestResult(
    Guid RequestId,
    string? RequestNumber
);

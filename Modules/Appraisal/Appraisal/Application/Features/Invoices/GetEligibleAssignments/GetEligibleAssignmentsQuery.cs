namespace Appraisal.Application.Features.Invoices.GetEligibleAssignments;

public record GetEligibleAssignmentsQuery(
    Guid CompanyId,
    string? SearchAppraisalNo = null,
    DateOnly? SubmittedDateFrom = null,
    DateOnly? SubmittedDateTo = null,
    Guid? CurrentInvoiceId = null
) : IQuery<IEnumerable<EligibleAssignmentDto>>;

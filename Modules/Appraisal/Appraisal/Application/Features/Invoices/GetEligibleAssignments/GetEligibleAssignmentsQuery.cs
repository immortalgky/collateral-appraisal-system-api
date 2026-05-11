namespace Appraisal.Application.Features.Invoices.GetEligibleAssignments;

public record GetEligibleAssignmentsQuery(Guid CompanyId) : IQuery<IEnumerable<EligibleAssignmentDto>>;

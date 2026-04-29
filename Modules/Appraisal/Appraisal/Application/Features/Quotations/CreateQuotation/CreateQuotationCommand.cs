using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.CreateQuotation;

public record CreateQuotationAppraisalDto(
    Guid AppraisalId,
    int? MaxAppraisalDays = null);

public record CreateQuotationCommand(
    DateTime DueDate,
    string RequestedBy,
    string? Description = null,
    string? SpecialRequirements = null,
    IReadOnlyList<CreateQuotationAppraisalDto>? Appraisals = null,
    IReadOnlyList<Guid>? InvitedCompanyIds = null
) : ICommand<CreateQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.CreateQuotation;

public record CreateQuotationCommand(
    DateTime DueDate,
    string RequestedBy,
    string? Description = null,
    string? SpecialRequirements = null
) : ICommand<CreateQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
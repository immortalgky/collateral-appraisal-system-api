using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.CreateQuotation;

public record CreateQuotationCommand(
    string QuotationNumber,
    DateTime DueDate,
    Guid RequestedBy,
    string RequestedByName,
    string? Description = null,
    string? SpecialRequirements = null
) : ICommand<CreateQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
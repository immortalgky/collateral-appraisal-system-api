namespace Appraisal.Application.Features.Quotations.CreateQuotation;

public class CreateQuotationCommandHandler(IQuotationRepository quotationRepository)
    : ICommandHandler<CreateQuotationCommand, CreateQuotationResult>
{
    public async Task<CreateQuotationResult> Handle(CreateQuotationCommand command, CancellationToken cancellationToken)
    {
        var quotation = QuotationRequest.Create(
            command.QuotationNumber,
            command.DueDate,
            command.RequestedBy,
            command.RequestedByName,
            command.Description);

        if (!string.IsNullOrWhiteSpace(command.SpecialRequirements))
            quotation.SetSpecialRequirements(command.SpecialRequirements);

        await quotationRepository.AddAsync(quotation, cancellationToken);

        return new CreateQuotationResult(quotation.Id);
    }
}
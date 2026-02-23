using Shared.CQRS;

namespace Integration.Application.Features.Quotations.ApproveQuotation;

public class ApproveQuotationCommandHandler
    : ICommandHandler<ApproveQuotationCommand, ApproveQuotationResult>
{
    public async Task<ApproveQuotationResult> Handle(
        ApproveQuotationCommand command,
        CancellationToken cancellationToken)
    {
        // This will be implemented once the quotation module is fully integrated
        // For now, return success as a placeholder
        await Task.CompletedTask;
        return new ApproveQuotationResult(true);
    }
}

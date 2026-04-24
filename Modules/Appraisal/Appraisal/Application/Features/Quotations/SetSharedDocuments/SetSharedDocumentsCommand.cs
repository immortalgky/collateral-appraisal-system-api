using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.SetSharedDocuments;

public record SetSharedDocumentsCommand(
    Guid QuotationRequestId,
    IReadOnlyList<SharedDocumentEntry> Documents)
    : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record SharedDocumentEntry(
    Guid AppraisalId,
    Guid DocumentId,
    string Level);

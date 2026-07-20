using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateAppraisalDocumentNotes;

public record UpdateAppraisalDocumentNotesCommand(
    Guid AppraisalId,
    Guid Id,
    string? Notes
) : ICommand<UpdateAppraisalDocumentNotesResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

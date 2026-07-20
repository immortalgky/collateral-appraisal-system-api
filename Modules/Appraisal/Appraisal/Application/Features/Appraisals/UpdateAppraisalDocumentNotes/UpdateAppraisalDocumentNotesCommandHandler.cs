using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.Appraisals.UpdateAppraisalDocumentNotes;

public class UpdateAppraisalDocumentNotesCommandHandler(
    IAppraisalDocumentRepository repository
) : ICommandHandler<UpdateAppraisalDocumentNotesCommand, UpdateAppraisalDocumentNotesResult>
{
    public async Task<UpdateAppraisalDocumentNotesResult> Handle(
        UpdateAppraisalDocumentNotesCommand command,
        CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAndAppraisalIdAsync(command.Id, command.AppraisalId, cancellationToken)
                       ?? throw new NotFoundException(nameof(AppraisalDocument), command.Id);

        document.UpdateNotes(command.Notes);

        await repository.UpdateAsync(document, cancellationToken);

        return new UpdateAppraisalDocumentNotesResult(document.Id, document.Notes);
    }
}

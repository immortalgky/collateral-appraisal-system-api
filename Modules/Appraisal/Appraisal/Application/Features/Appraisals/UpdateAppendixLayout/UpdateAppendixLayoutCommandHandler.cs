using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.Appraisals.UpdateAppendixLayout;

public class UpdateAppendixLayoutCommandHandler(
    IAppraisalAppendixRepository repository
) : ICommandHandler<UpdateAppendixLayoutCommand, UpdateAppendixLayoutResult>
{
    public async Task<UpdateAppendixLayoutResult> Handle(
        UpdateAppendixLayoutCommand command,
        CancellationToken cancellationToken)
    {
        var appendix = await repository.GetByIdAsync(command.AppendixId, cancellationToken)
                       ?? throw new NotFoundException(nameof(AppraisalAppendix), command.AppendixId);

        appendix.UpdateLayout(command.LayoutColumns);
        await repository.UpdateAsync(appendix, cancellationToken);

        return new UpdateAppendixLayoutResult(appendix.Id, appendix.LayoutColumns);
    }
}

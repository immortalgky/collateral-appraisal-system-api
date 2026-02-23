using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateAppendixLayout;

public record UpdateAppendixLayoutCommand(
    Guid AppendixId,
    int LayoutColumns
) : ICommand<UpdateAppendixLayoutResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

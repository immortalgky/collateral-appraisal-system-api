using MediatR;

namespace Collateral.Application.Features.CollateralMasters.Documents.ArchiveDocument;

public class ArchiveCollateralDocumentCommandHandler(
    ICollateralMasterRepository repository
) : ICommandHandler<ArchiveCollateralDocumentCommand>
{
    public async Task<Unit> Handle(
        ArchiveCollateralDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var master = await repository.FindByIdAsync(command.CollateralMasterId, cancellationToken);
        if (master is null)
            throw new NotFoundException("CollateralMaster", command.CollateralMasterId);

        // ArchiveDocument throws NotFoundException when the document row is not found or already archived.
        master.ArchiveDocument(command.DocumentRowId);

        await repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

namespace Collateral.Application.Features.CollateralMasters.Documents.AttachDocument;

public class AttachCollateralDocumentCommandHandler(
    ICollateralMasterRepository repository
) : ICommandHandler<AttachCollateralDocumentCommand, AttachCollateralDocumentResult>
{
    public async Task<AttachCollateralDocumentResult> Handle(
        AttachCollateralDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // FindByIdAsync already filters !IsDeleted — deleted masters surface as not-found.
        var master = await repository.FindByIdAsync(command.CollateralMasterId, cancellationToken);
        if (master is null)
            throw new NotFoundException("CollateralMaster", command.CollateralMasterId);

        var document = master.AttachDocument(
            command.DocumentType,
            command.DocumentId,
            command.FileName,
            command.Description);

        await repository.SaveChangesAsync(cancellationToken);

        return new AttachCollateralDocumentResult(document.Id);
    }
}

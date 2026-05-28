namespace Appraisal.Application.Features.SupportingDataMaintenance.BulkUploadSupportingDetails;

/// <summary>
/// Command to bulk-insert supporting details parsed from an Excel file.
/// The file stream is passed directly from the HTTP request — no file metadata is stored in the DB.
/// The ITransactionalCommand marker tells the pipeline to wrap the handler in one DB transaction.
/// </summary>
public record BulkUploadSupportingDetailsCommand(
    Guid SupportingId,
    Stream FileStream
) : ICommand<BulkUploadSupportingDetailsResult>,
    ITransactionalCommand<IAppraisalUnitOfWork>;

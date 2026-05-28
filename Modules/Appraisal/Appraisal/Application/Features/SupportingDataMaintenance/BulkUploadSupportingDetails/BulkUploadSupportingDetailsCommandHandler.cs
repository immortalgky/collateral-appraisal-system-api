namespace Appraisal.Application.Features.SupportingDataMaintenance.BulkUploadSupportingDetails;

internal class BulkUploadSupportingDetailsCommandHandler(
    ISupportingDataRepository repo,
    ICurrentUserService currentUserService
) : ICommandHandler<BulkUploadSupportingDetailsCommand, BulkUploadSupportingDetailsResult>
{
    public async Task<BulkUploadSupportingDetailsResult> Handle(
        BulkUploadSupportingDetailsCommand cmd,
        CancellationToken ct)
    {
        var hasEditPermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT"); // check permission to create new supporting data

        // 1. Permission guard (same rule as single-row create)
        if (!hasEditPermission)
        {
            throw new UnauthorizedAccessException(
                "You are not allowed to upload supporting details.");
        }

        // 2. Load the aggregate (with details, since we'll be adding to the collection)
        var supportingData = await repo.GetByIdWithDetailsAsync(cmd.SupportingId, ct)
            ?? throw new NotFoundException(
                $"Supporting data with ID {cmd.SupportingId} not found.");

        // 3. Parse Excel (throws BulkUploadParseException if any row fails) ─
        // All-or-nothing: if even one row is invalid, we never reach step 4.
        var rows = SupportingDetailExcelParser.Parse(cmd.FileStream);

        // 4. Insert every valid row into the aggregate ───────────────────
        // AddDetail only touches in-memory collections — no DB calls here.
        foreach (var row in rows)
            supportingData.AddDetail(row);

        // 5. No SaveChangesAsync here — ITransactionalCommand pipeline handles it

        return new BulkUploadSupportingDetailsResult(rows.Count);
    }
}

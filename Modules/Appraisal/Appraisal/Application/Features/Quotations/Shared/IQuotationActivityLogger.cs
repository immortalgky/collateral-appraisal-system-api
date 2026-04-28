namespace Appraisal.Application.Features.Quotations.Shared;

/// <summary>
/// Stages a <see cref="Domain.Quotations.QuotationActivityLog"/> row onto the current DbContext.
/// The entry is committed atomically with the surrounding business operation — callers must NOT
/// call SaveChanges themselves; the repository update flushes both together.
/// </summary>
public interface IQuotationActivityLogger
{
    void Log(
        Guid quotationRequestId,
        Guid? companyQuotationId,
        Guid? companyId,
        string activityName,
        string? remark = null,
        string? actionByRole = null);
}

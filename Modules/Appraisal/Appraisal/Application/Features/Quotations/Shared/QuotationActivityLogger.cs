using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.Shared;

internal sealed class QuotationActivityLogger(AppraisalDbContext db, ICurrentUserService currentUser)
    : IQuotationActivityLogger
{
    public void Log(
        Guid quotationRequestId,
        Guid? companyQuotationId,
        Guid? companyId,
        string activityName,
        string? remark = null,
        string? actionByRole = null)
    {
        var actionBy = currentUser.Username
                       ?? currentUser.UserId?.ToString()
                       ?? "system";
        var role = actionByRole ?? currentUser.Roles.FirstOrDefault();

        var entry = QuotationActivityLog.Create(
            quotationRequestId,
            companyQuotationId,
            companyId,
            activityName,
            actionBy,
            role,
            remark);

        db.QuotationActivityLogs.Add(entry);
    }
}

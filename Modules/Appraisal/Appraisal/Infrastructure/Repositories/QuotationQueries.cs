using Appraisal.Domain.Quotations;

namespace Appraisal.Infrastructure.Repositories;

internal static class QuotationQueries
{
    /// <summary>
    /// The latest finalized quotation of the appraisal, newest FINALIZED first (SelectedAt), not the newest draft.
    /// Deliberately NOT filtered by company: the current round decides who is billed. Shared by the fee lookup, the
    /// assign guard and the fee preview so they cannot pick different quotations. The ordering ends in Id, so it
    /// is unique.
    /// </summary>
    public static IOrderedQueryable<QuotationRequest> LatestFinalized(
        this IQueryable<QuotationRequest> quotations, Guid appraisalId) =>
        quotations
            .Where(q => q.Status == "Finalized" && q.Appraisals.Any(a => a.AppraisalId == appraisalId))
            .OrderByDescending(q => q.SelectedAt)
            .ThenByDescending(q => q.RequestDate)
            .ThenBy(q => q.Id);
}

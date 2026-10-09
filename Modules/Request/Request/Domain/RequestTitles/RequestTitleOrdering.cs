namespace Request.Domain.RequestTitles;

public static class RequestTitleOrdering
{
    /// <summary>
    /// The order the requester listed the titles in. Rows that predate SequenceNumber are all 0 and fall
    /// back to creation order (the Guid v7 Id alone does not sort by time in SQL Server).
    /// </summary>
    public static IQueryable<RequestTitle> InDisplayOrder(this IQueryable<RequestTitle> titles) =>
        titles.OrderBy(t => t.SequenceNumber).ThenBy(t => t.CreatedAt).ThenBy(t => t.Id);
}

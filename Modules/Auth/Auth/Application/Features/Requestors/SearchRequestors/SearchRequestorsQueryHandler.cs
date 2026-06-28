using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Features.Requestors.SearchRequestors;

internal class SearchRequestorsQueryHandler(
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext)
    : IQueryHandler<SearchRequestorsQuery, SearchRequestorsResult>
{
    public async Task<SearchRequestorsResult> Handle(SearchRequestorsQuery query, CancellationToken cancellationToken)
    {
        var q = userManager.Users
            .Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(u =>
                u.UserName!.Contains(query.Search) ||
                u.FirstName.Contains(query.Search) ||
                u.LastName.Contains(query.Search) ||
                (u.Email != null && u.Email.Contains(query.Search)));

        var total = await q.LongCountAsync(cancellationToken);

        var pageSize = query.PageSize;
        var pageNumber = query.PageNumber;

        var users = await q
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip(Math.Max(pageNumber - 1, 0) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
            return new SearchRequestorsResult([], total, pageNumber, pageSize);

        var items = await BuildRequestorItemsAsync(users, cancellationToken);

        return new SearchRequestorsResult(items, total, pageNumber, pageSize);
    }

    /// <summary>
    /// Projects a page of users into <see cref="RequestorItemDto"/> by joining the Officer and
    /// CostCenter reference tables. Both joins are left-outer so the result is never filtered out
    /// when reference data is missing or the AO-code / cost-center-code mismatch applies.
    /// </summary>
    internal async Task<List<RequestorItemDto>> BuildRequestorItemsAsync(
        List<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var aoCodes = users
            .Where(u => u.AoCode != null)
            .Select(u => u.AoCode!)
            .Distinct()
            .ToList();

        // Left-join Officers on AoCode. Tolerate empty aoCodes list.
        var officerMap = aoCodes.Count == 0
            ? new Dictionary<string, (string? CostCenterCode, bool IsActive)>(StringComparer.OrdinalIgnoreCase)
            : await dbContext.Officers
                .Where(o => aoCodes.Contains(o.OfficerCode))
                .ToDictionaryAsync(
                    o => o.OfficerCode,
                    o => (o.CostCenterCode, o.IsActive),
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);

        // Left-join CostCenters on Officer.CostCenterCode. There is a known 8-digit vs 3-digit
        // mismatch between Officer.CostCenterCode (SSONTH) and CostCenter.Code (G7CNTR);
        // the join will often yield no match — that is expected and not an error.
        var costCenterCodes = officerMap.Values
            .Where(v => v.CostCenterCode != null)
            .Select(v => v.CostCenterCode!)
            .Distinct()
            .ToList();

        var costCenterMap = costCenterCodes.Count == 0
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : await dbContext.CostCenters
                .Where(c => costCenterCodes.Contains(c.Code))
                .ToDictionaryAsync(
                    c => c.Code,
                    c => c.Description,
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);

        return users.Select(u =>
        {
            var costCenterCode = u.AoCode != null && officerMap.TryGetValue(u.AoCode, out var officer)
                ? officer.CostCenterCode
                : null;

            var costCenterDesc = costCenterCode != null && costCenterMap.TryGetValue(costCenterCode, out var desc)
                ? desc
                : null;

            return new RequestorItemDto(
                EmployeeId: u.UserName ?? "",
                Name: $"{u.FirstName} {u.LastName}".Trim(),
                Email: u.Email,
                ContactNo: u.PhoneNumber,
                AoCode: u.AoCode,
                CostCenterCode: costCenterCode,
                CostCenterDescription: costCenterDesc,
                Department: u.Department);
        }).ToList();
    }
}

using Appraisal.Application.Features.Shared;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

/// <summary>
/// Handler for getting an Appraisal by ID.
/// Uses SQL view + Dapper for efficient read queries.
/// </summary>
public class GetAppraisalByIdQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetAppraisalByIdQuery, GetAppraisalByIdResult>
{
    public async Task<GetAppraisalByIdResult> Handle(
        GetAppraisalByIdQuery query,
        CancellationToken cancellationToken)
    {
        /*
         * ⚠ NOT company-scoped, unlike the list, the export, the quick search and the brief.
         *
         * An external firm's user can read any appraisal by id here — facility limit, appraised
         * value, appraiser, SLA — because this endpoint carries only the global login fallback.
         * That predates this feature, and a company predicate was tried and reverted: the right
         * rule is not "which company holds an assignment".
         *
         * Why an assignment-based scope does not work. `Appraisal.AssignAdmin()` creates an
         * Internal assignment with a NULL company at creation, and a firm invited to QUOTE has no
         * AppraisalAssignments row of its own at all — the invitation lives in the workflow task,
         * whose `AssigneeCompanyId` is NULL on all 105k rows, so the link to the firm is the
         * assigned USER. Both the latest-only and any-live forms therefore 404'd an invited firm
         * on the very task page it was invited to, and TaskLayout turns that into a full-screen
         * failure.
         *
         * Closing it properly means authorising on the task ("may this user see this appraisal")
         * rather than on the company, across the 13 call sites that use this read. Tracked as
         * backlog in .claude/tasks/credit-appraisal-tracking.md rather than guessed at here.
         */
        const string sql = """SELECT * FROM appraisal.vw_AppraisalDetail WHERE Id = @Id""";

        var result = await connectionFactory.QueryFirstOrDefaultAsync<GetAppraisalByIdResult>(
            sql,
            new { query.Id });

        if (result is null)
            throw new AppraisalNotFoundException(query.Id);

        // Same rule as the list: a credit-side caller gets the row, not the internal columns,
        // and not the value until the committee has approved it. See AppraisalFieldScope.
        return AppraisalFieldScope.IsTrackingOnly(currentUser)
            ? AppraisalFieldScope.Mask(result)
            : result;
    }
}

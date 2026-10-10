using Appraisal.Application.Features.Appraisals.GetAppraisalRequest;
using Appraisal.Application.Features.Shared;
using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using MediatR;
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
    ICurrentUserService currentUser,
    ISender sender
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
        // With an include (or on the Integration route) the same release rule also covers a caller with NO appraisal
        // permission, such as an Integration client token: it must not read an unreleased value either. The plain
        // header on the Appraisal route is not tightened for them: RequestMaker / RequestChecker hold no appraisal
        // permission and legitimately open the workspace from a task (see the router comment).
        var strict = query.StrictRelease || query.Include != AppraisalInclude.None;
        if (AppraisalFieldScope.IsTrackingOnly(currentUser)
            || (strict && AppraisalFieldScope.WithholdsUnreleased(currentUser, result.Status)))
            result = AppraisalFieldScope.Mask(result);

        // Opt-in parts. Nothing below runs for the default read, so every appraisal page stays one query.
        if (query.Include == AppraisalInclude.None)
            return result;

        // The header above is deliberately not company-scoped (see the note at the top), but the request data and
        // files are: an external (company) caller gets 404 unless the appraisal is assigned to its company.
        await AppraisalAccessScope.EnsureCallerMayReadAsync(
            connectionFactory.GetOpenConnection(), currentUser, query.Id, cancellationToken);

        // Until the appraisal is released, only a full appraisal viewer gets the download ids and paths. (The request
        // part needs no masking: its prior book is a past appraisal's, not this appraisal's unreleased value.)
        var withhold = AppraisalFieldScope.WithholdsUnreleased(currentUser, result.Status);

        // A part that cannot be read (the request is soft-deleted or has no detail) is left empty: the header
        // was found, so the response is still a 200.
        if (query.Include.HasFlag(AppraisalInclude.Request))
        {
            try
            {
                result.Request = await sender.Send(new GetAppraisalRequestQuery(query.Id), cancellationToken);
            }
            catch (AppraisalNotFoundException)
            {
                result.Request = null;
            }
        }

        if (query.Include.HasFlag(AppraisalInclude.Documents))
        {
            try
            {
                // Any status: the consumer reads it from the header.
                var files = await sender.Send(new GetCarryForwardDocumentsQuery(query.Id, AnyStatus: true), cancellationToken);
                result.Documents = files.Documents.Select(d => AppraisalDocumentDto.From(d, withhold)).ToList();
            }
            catch (AppraisalNotFoundException)
            {
                result.Documents = [];
            }
        }

        return result;
    }
}

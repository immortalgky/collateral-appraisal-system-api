using Collateral.CollateralMasters.Reappraisal;
using Collateral.Data;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Collateral.CollateralMasters.Consumers;

/// <summary>
/// Marks an AS400 reappraisal book as reviewed (<see cref="ReappraisalCandidate.MarkConsumed"/>) when a
/// reappraisal request for it is submitted — whether Initiate created the request or staff raised it by hand.
///
/// Consumed on SUBMIT, not on Initiate: Initiate only creates a request for staff to review, and a
/// request deleted before it is sent must leave the book on the to-do list. While the request waits,
/// the list shows the book as in progress and Initiate refuses it again.
///
/// The book is, in order:
///   1. the one Initiate stamped on the request (Request.ReappraisalBookNumber, with GroupTag) — not
///      the form's prior-appraisal fields, which staff can edit;
///   2. for a reappraisal raised by hand (purpose 03 / block 09), the number of the prior appraisal it
///      points at (PrevAppraisalId) — the same rule as appraisal.vw_ReappraisalsByBook arm 1. Without it
///      the book stayed on the to-do list after its reappraisal completed.
/// Every not-yet-consumed row of that book is consumed, under whichever collateral it is listed —
/// except a block-project unit (ReappraisalCandidate.IsBlockUnit), where each collateral is a different
/// unit: only the row of the collateral Initiate stamped (Request.ReappraisalCollateralId), and nothing
/// for a reappraisal raised by hand, which points at the project and cannot say which unit it reviewed.
/// </summary>
public class RequestSubmittedReappraisalConsumer(
    CollateralDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    ILogger<RequestSubmittedReappraisalConsumer> logger,
    InboxGuard<CollateralDbContext> inboxGuard)
    : IConsumer<RequestSubmittedIntegrationEvent>
{
    // Initiate's requests are recognised by GroupTag, never Channel: Initiate persists it on every request
    // it creates, and Submit forwards it. Channel is editable on the form, and an API request carrying
    // Channel "SIBS" also gets ExternalSystem "SIBS" (CreateAndSubmitRequestAsync) — keyed on that, an LOS
    // progressive or appeal request would consume a due book. A request raised by hand qualifies only with
    // a reappraisal purpose and a prior appraisal: a progressive inspection or an appeal also points back
    // at a book but reviews nothing.
    public Task Consume(ConsumeContext<RequestSubmittedIntegrationEvent> context) =>
        context.Message.GroupTag is not null || IsManualReappraisal(context.Message)
            ? inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken)
            : Task.CompletedTask;

    private static bool IsManualReappraisal(RequestSubmittedIntegrationEvent msg) =>
        msg.Purpose is "03" or "09" && msg.PrevAppraisalId is not null;

    private async Task HandleAsync(ConsumeContext<RequestSubmittedIntegrationEvent> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        // The book Initiate stamped on the request first — a system field, unaffected by staff clearing the
        // prior-appraisal fields on the form before submitting. Otherwise, for a reappraisal raised by
        // hand, the prior appraisal's number. (A block-reappraisal request carries a GroupTag but no AS400
        // book, so it falls to the second arm through its prior appraisal.)
        const string sql = """
            SELECT r.ReappraisalBookNumber AS Number, r.ReappraisalCollateralId AS CollateralId, CAST(1 AS bit) AS FromInitiate
            FROM request.Requests r
            WHERE r.Id = @RequestId
              AND r.ReappraisalBookNumber IS NOT NULL
            UNION ALL
            SELECT prev.AppraisalNumber, NULL, CAST(0 AS bit)
            FROM appraisal.Appraisals prev
            WHERE prev.Id = @PrevAppraisalId
              AND prev.IsDeleted = 0
              AND @IsReappraisal = 1
            ORDER BY FromInitiate DESC
            """;

        var link = await connectionFactory.GetOpenConnection()
            .QueryFirstOrDefaultAsync<BookLink>(sql, new
            {
                msg.RequestId,
                msg.PrevAppraisalId,
                IsReappraisal = IsManualReappraisal(msg),
            });
        var number = link?.Number;

        if (number is null)
        {
            logger.LogInformation(
                "[REAPPRAISAL-SUBMITTED] Request {RequestId} is not linked to an AS400 book; nothing to consume",
                msg.RequestId);
            return;
        }

        var candidates = await dbContext.ReappraisalCandidates
            // Every collateral the book is listed under: the request covers the whole book, and the
            // waiting / in-progress checks are by book too.
            .Where(c => c.NormalizedSurveyNumber == number
                        // Deleted too: a book skipped after Initiate but submitted anyway IS reviewed.
                        && c.Status != ReappraisalCandidateStatus.Consumed
                        // A block-project unit: only the unit (collateral) this request was raised for.
                        && (!c.IsBlockUnit || (link!.FromInitiate && c.CollateralId == link.CollateralId)))
            .ToListAsync(ct);

        foreach (var candidate in candidates)
            candidate.MarkConsumed();

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "[REAPPRAISAL-SUBMITTED] Request {RequestId}: consumed {Count} candidate(s) for book {Number}",
            msg.RequestId, candidates.Count, number);
    }

    private sealed record BookLink(string Number, string? CollateralId, bool FromInitiate);
}

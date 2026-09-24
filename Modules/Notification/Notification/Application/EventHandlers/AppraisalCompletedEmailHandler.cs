using Auth.Contracts.Users;
using Dapper;
using MassTransit;
using Notification.Contracts.Email;
using Notification.Data;
using Notification.Infrastructure.Email;
using Notification.Infrastructure.Email.Templates;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Application.EventHandlers;

/// <summary>
/// Emails the RM (the request's Requestor) once a <b>New</b> appraisal is approved and completed.
///
/// Listens to <see cref="AppraisalCompletedIntegrationEvent"/>, not AppraisalResultReadyIntegrationEvent:
/// the result-ready event is re-published every time an admin regenerates the summary, which would
/// re-send this email. The completion event fires once per approval.
/// </summary>
public sealed class AppraisalCompletedEmailHandler(
    IEmailSender emailSender,
    IEmailTemplateRenderer templateRenderer,
    IUserLookupService userLookupService,
    ISqlConnectionFactory connectionFactory,
    InboxGuard<NotificationDbContext> inboxGuard,
    ILogger<AppraisalCompletedEmailHandler> logger)
    : IConsumer<AppraisalCompletedIntegrationEvent>
{
    private const string NewAppraisalType = "New";
    private const string DefaultChannel = "CLS";
    private const int MaxCustomerNamesInBody = 4;
    private const int MaxCustomerNamesInSubject = 1;

    private const string Sql = """
        SELECT
            a.AppraisalType AS AppraisalType,
            a.AppraisalNumber AS AppraisalNumber,
            r.Channel AS Channel,
            r.Requestor AS Requestor,
            r.Id AS RequestId
        FROM appraisal.Appraisals a
        JOIN request.Requests r ON r.Id = a.RequestId
        WHERE a.Id = @AppraisalId
        """;

    // Id is an identity column, so it follows the borrower order the customers were entered in.
    private const string CustomersSql = """
        SELECT c.Name
        FROM request.RequestCustomers c
        WHERE c.RequestId = @RequestId
        ORDER BY c.Id
        """;

    public Task Consume(ConsumeContext<AppraisalCompletedIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, async ct =>
        {
            var msg = context.Message;

            var connection = connectionFactory.GetOpenConnection();
            var row = await connection.QuerySingleOrDefaultAsync<AppraisalRow>(
                new CommandDefinition(Sql, new { msg.AppraisalId }, cancellationToken: ct));

            if (row is null || !string.Equals(row.AppraisalType, NewAppraisalType, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Skipping appraisal-completed email: AppraisalId={AppraisalId} not found or not a New appraisal (Type={AppraisalType})",
                    msg.AppraisalId, row?.AppraisalType);
                return;
            }

            var rm = string.IsNullOrWhiteSpace(row.Requestor)
                ? null
                : await userLookupService.GetRequestorAsync(row.Requestor, ct);

            if (string.IsNullOrWhiteSpace(rm?.Email))
            {
                logger.LogWarning(
                    "Skipping appraisal-completed email: no RM email for Requestor={Requestor} (AppraisalId={AppraisalId})",
                    row.Requestor, msg.AppraisalId);
                return;
            }

            var customerNames = (await connection.QueryAsync<string?>(
                new CommandDefinition(CustomersSql, new { row.RequestId }, cancellationToken: ct))).ToList();

            var channel = string.IsNullOrWhiteSpace(row.Channel) ? DefaultChannel : row.Channel;
            // Subject names only the main borrower to stay short; the body lists up to four.
            var subjectCustomers = FormatCustomerNames(customerNames, MaxCustomerNamesInSubject);
            var subject = $"แจ้งผลการประเมินหลักประกันเสร็จสิ้น – ลูกค้า {subjectCustomers ?? "-"}";
            var model = new AppraisalCompletedNoticeModel(
                rm.Name, FormatCustomerNames(customerNames, MaxCustomerNamesInBody), row.AppraisalNumber, channel);

            await emailSender.SendAsync(new EmailMessage(
                Subject: subject,
                HtmlBody: templateRenderer.AppraisalCompletedNotice(subject, model),
                To: [rm.Email],
                Source: "AppraisalCompleted",
                ReferenceId: msg.AppraisalId.ToString()), ct);
        }, context.CancellationToken);

    /// <summary>
    /// Borrowers in entry order, comma-separated, capped at <paramref name="max"/>;
    /// the rest are summarised as "และอีก N ราย". Null when the request has no named customer.
    /// </summary>
    internal static string? FormatCustomerNames(IEnumerable<string?> names, int max)
    {
        var list = names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()).ToList();
        if (list.Count == 0) return null;

        var shown = string.Join(", ", list.Take(max));
        var remaining = list.Count - max;
        return remaining > 0 ? $"{shown} และอีก {remaining} ราย" : shown;
    }

    private sealed class AppraisalRow
    {
        public string? AppraisalType { get; init; }
        public string? AppraisalNumber { get; init; }
        public string? Channel { get; init; }
        public string? Requestor { get; init; }
        public Guid RequestId { get; init; }
    }
}

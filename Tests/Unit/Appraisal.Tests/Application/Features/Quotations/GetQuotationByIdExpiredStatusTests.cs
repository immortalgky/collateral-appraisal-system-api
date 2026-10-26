using System.Data;
using System.Data.Common;
using Appraisal.Application.Features.Quotations.AutoAssign;
using Appraisal.Application.Features.Quotations.GetQuotationById;
using Appraisal.Contracts.Services;
using Appraisal.Domain.Quotations;
using NSubstitute;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Tests.Application.Features.Quotations;

/// <summary>
/// A Declined row whose invitation expired reads "Expired" with no submission time, number, price or draft data (its
/// stored status stays Declined); a decline by choice keeps "Declined", and a real bid is never relabelled.
/// </summary>
public class GetQuotationByIdExpiredStatusTests
{
    [Fact]
    public async Task Expired_invitation_reads_Expired_without_submission_or_price_data_but_a_normal_decline_stays_Declined()
    {
        var declinedAt = new DateTime(2026, 10, 1, 9, 0, 0);
        var quotation = QuotationRequest.Create(declinedAt.AddDays(7), "rm", declinedAt);

        var expiredInvitation = quotation.InviteCompany(Guid.NewGuid());
        expiredInvitation.Id = Guid.NewGuid();
        expiredInvitation.MarkExpired();
        var declinedInvitation = quotation.InviteCompany(Guid.NewGuid());
        declinedInvitation.Id = Guid.NewGuid();
        declinedInvitation.Decline();
        var bidInvitation = quotation.InviteCompany(Guid.NewGuid());
        bidInvitation.Id = Guid.NewGuid();
        bidInvitation.MarkExpired();
        var neverOpenedInvitation = quotation.InviteCompany(Guid.NewGuid());
        neverOpenedInvitation.Id = Guid.NewGuid();
        neverOpenedInvitation.MarkExpired();
        var pendingInvitation = quotation.InviteCompany(Guid.NewGuid());
        pendingInvitation.Id = Guid.NewGuid();
        pendingInvitation.MarkExpired();
        var draftDeclinedInvitation = quotation.InviteCompany(Guid.NewGuid());
        draftDeclinedInvitation.Id = Guid.NewGuid();
        draftDeclinedInvitation.Decline();

        // Auto-expired Draft with unsent figures: SubmittedAt is still default(DateTime).
        var expired = CompanyQuotation.CreateDraft(quotation.Id, expiredInvitation.Id, expiredInvitation.CompanyId, "Q-1");
        expired.Id = Guid.NewGuid();
        expired.AddItem(Guid.NewGuid(), Guid.NewGuid(), 1, 1000m, 5);
        expired.SetRemarks("unsent");
        expired.SetValidUntil(declinedAt.AddDays(30));
        expired.SetProposedDates(declinedAt.AddDays(1), declinedAt.AddDays(10));
        expired.Decline("expired", "SYSTEM", declinedAt);
        var declined = CompanyQuotation.CreateDeclined(
            quotation.Id, declinedInvitation.Id, declinedInvitation.CompanyId, "Q-2", "no capacity", "company", declinedAt);
        declined.Id = Guid.NewGuid();
        // Maker's bid awaiting the checker, auto-declined at cutoff with real prices and items.
        var pending = CompanyQuotation.CreateDraft(
            quotation.Id, pendingInvitation.Id, pendingInvitation.CompanyId, "Q-5");
        pending.Id = Guid.NewGuid();
        pending.AddItem(Guid.NewGuid(), Guid.NewGuid(), 1, 2000m, 7);
        pending.MarkPendingCheckerReview("maker");
        pending.Decline("expired", "SYSTEM", declinedAt);
        // Guards the cq.Status == "Declined" condition; a non-Declined row under an Expired invitation can't happen in prod.
        var bid = CompanyQuotation.CreateDraft(quotation.Id, bidInvitation.Id, bidInvitation.CompanyId, "Q-3");
        bid.Id = Guid.NewGuid();
        bid.MarkSubmitted(declinedAt);
        // Declined by choice from a Draft: SubmittedAt is still default(DateTime).
        var declinedDraft = CompanyQuotation.CreateDraft(
            quotation.Id, draftDeclinedInvitation.Id, draftDeclinedInvitation.CompanyId, "Q-4");
        declinedDraft.Id = Guid.NewGuid();
        declinedDraft.Decline("busy", "company", declinedAt);
        // Company that never opened the invitation: the auto-decline job creates a Declined row for it.
        var neverOpened = CompanyQuotation.CreateDeclined(
            quotation.Id, neverOpenedInvitation.Id, neverOpenedInvitation.CompanyId,
            $"EXPIRED-{neverOpenedInvitation.CompanyId:N}", "expired", "SYSTEM", declinedAt);
        neverOpened.Id = Guid.NewGuid();
        quotation.AddQuotation(expired);
        quotation.AddQuotation(neverOpened);
        quotation.AddQuotation(pending);
        quotation.AddQuotation(bid);
        quotation.AddQuotation(declinedDraft);
        quotation.AddQuotation(declined);

        var result = await BuildHandler(quotation).Handle(
            new GetQuotationByIdQuery(quotation.Id), TestContext.Current.CancellationToken);

        var expiredRow = result.CompanyQuotations.Single(c => c.Id == expired.Id);
        Assert.Equal("Expired", expiredRow.Status);
        Assert.Null(expiredRow.SubmittedAt);
        Assert.Null(expiredRow.TotalQuotedPrice);
        Assert.Null(expiredRow.OriginalQuotedPrice);
        Assert.Null(expiredRow.CurrentNegotiatedPrice);
        Assert.Empty(expiredRow.Items);
        Assert.Equal(0, expiredRow.EstimatedDays);
        Assert.Null(expiredRow.QuotationNumber);
        Assert.Null(expiredRow.Remarks);
        Assert.Null(expiredRow.ValidUntil);
        Assert.Null(expiredRow.ProposedStartDate);
        Assert.Null(expiredRow.ProposedCompletionDate);

        var pendingRow = result.CompanyQuotations.Single(c => c.Id == pending.Id);
        Assert.Equal("Expired", pendingRow.Status);
        Assert.Null(pendingRow.SubmittedAt);
        Assert.Null(pendingRow.QuotationNumber);
        Assert.Null(pendingRow.TotalQuotedPrice);
        Assert.Empty(pendingRow.Items);

        var neverOpenedRow = result.CompanyQuotations.Single(c => c.Id == neverOpened.Id);
        Assert.Equal("Expired", neverOpenedRow.Status);
        Assert.Null(neverOpenedRow.SubmittedAt);
        Assert.Null(neverOpenedRow.QuotationNumber);

        var declinedRow = result.CompanyQuotations.Single(c => c.Id == declined.Id);
        Assert.Equal("Declined", declinedRow.Status);
        Assert.Equal(declinedAt, declinedRow.SubmittedAt);

        var bidRow = result.CompanyQuotations.Single(c => c.Id == bid.Id);
        Assert.Equal("Submitted", bidRow.Status);
        Assert.Equal(declinedAt, bidRow.SubmittedAt);
        Assert.Equal("Q-3", bidRow.QuotationNumber);

        var declinedDraftRow = result.CompanyQuotations.Single(c => c.Id == declinedDraft.Id);
        Assert.Equal("Declined", declinedDraftRow.Status);
        Assert.Null(declinedDraftRow.SubmittedAt);
    }

    private static GetQuotationByIdQueryHandler BuildHandler(QuotationRequest quotation)
    {
        var repository = Substitute.For<IQuotationRepository>();
        repository.GetByIdWithNegotiationsAsync(quotation.Id, Arg.Any<CancellationToken>()).Returns(quotation);
        var user = Substitute.For<ICurrentUserService>();
        user.IsInRole("Admin").Returns(true);
        var factory = Substitute.For<ISqlConnectionFactory>();
        factory.GetOpenConnection().Returns(new EmptyConnection());
        return new GetQuotationByIdQueryHandler(
            repository, user, factory, Substitute.For<IQuotationTaskOwnershipService>(),
            Substitute.For<IQuotationAutoAssignStateReader>());
    }

    // Dapper needs a DbConnection for its async calls; every lookup (company names, etc.) returns no rows.
    private sealed class EmptyConnection : DbConnection
    {
        public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new EmptyCommand();
    }

    private sealed class EmptyCommand : DbCommand
    {
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = Substitute.For<DbParameterCollection>();
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => Substitute.For<DbParameter>();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new DataTable().CreateDataReader();
    }
}

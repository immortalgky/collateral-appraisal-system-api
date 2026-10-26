using Appraisal.Application.Features.Quotations.Shared;
using Auth.Contracts.Companies;
using Shared.Configuration;

namespace Appraisal.Application.Features.Quotations.AutoAssign;

public record GetAutoAssignPreviewQuery(Guid QuotationRequestId) : IQuery<AutoAssignPreviewResult>;

public record AutoAssignPreviewResult(
    bool ExternalAssignmentEnabled,
    bool Standalone,
    IReadOnlyList<AutoAssignPreviewAppraisal> Appraisals);

public record AutoAssignPreviewAppraisal(
    Guid AppraisalId,
    string? AppraisalNumber,
    Guid? WorkflowInstanceId,
    string? CurrentActivityId,
    bool IsPma,
    string? TaskHeldBy,
    string PredictedOutcome,
    string? Reason);

public class GetAutoAssignPreviewQueryHandler(
    IQuotationAutoAssignStateReader stateReader,
    ISystemConfigurationReader configReader,
    ICompanyLookupService companyLookup,
    ICurrentUserService currentUser)
    : IQueryHandler<GetAutoAssignPreviewQuery, AutoAssignPreviewResult>
{
    public async Task<AutoAssignPreviewResult> Handle(
        GetAutoAssignPreviewQuery query,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var appraisalIds = await stateReader.GetQuotationAppraisalIdsAsync(query.QuotationRequestId, cancellationToken)
                           ?? throw new NotFoundException($"Quotation '{query.QuotationRequestId}' not found");

        var states = await stateReader.GetAsync(appraisalIds, cancellationToken);

        // Same winner rules as the post-finalize loop, applied to the tentative winner before finalize.
        var winner = await stateReader.GetWinnerAsync(query.QuotationRequestId, cancellationToken);
        var company = winner.CompanyId is { } companyId
            ? await companyLookup.GetByIdAsync(companyId, cancellationToken)
            : null;
        var priced = winner.CompanyQuotationId is { } companyQuotationId
            ? (await stateReader.GetPricedAppraisalIdsAsync(companyQuotationId, cancellationToken)).ToHashSet()
            : [];
        var user = currentUser.UserCode;
        var winnerAssignable = company is { IsAssignable: true };

        return new AutoAssignPreviewResult(
            await configReader.GetBoolAsync(
                AutoAssignOutcome.ExternalAssignmentEnabledKey, true, cancellationToken),
            await stateReader.IsStandaloneAsync(query.QuotationRequestId, cancellationToken),
            states.Select(s =>
            {
                var decision = AutoAssignClassifier.Classify(s, winnerAssignable, priced.Contains(s.AppraisalId), user);
                return new AutoAssignPreviewAppraisal(
                    s.AppraisalId,
                    s.AppraisalNumber,
                    s.WorkflowInstanceId,
                    s.PendingActivityId,
                    s.IsPma,
                    s.LockedByOther(user) ?? s.AssignedToOther(user),
                    decision.Outcome ?? "Ready",
                    decision.Reason);
            }).ToList());
    }
}

public class GetAutoAssignPreviewEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/quotations/{id:guid}/auto-assign-preview",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new GetAutoAssignPreviewQuery(id), cancellationToken)))
            .WithName("GetQuotationAutoAssignPreview")
            .Produces<AutoAssignPreviewResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Preview quotation auto-assign")
            .WithDescription("Admin only. Shows which appraisals of the quotation are ready to be auto-assigned to the winner on finalize.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

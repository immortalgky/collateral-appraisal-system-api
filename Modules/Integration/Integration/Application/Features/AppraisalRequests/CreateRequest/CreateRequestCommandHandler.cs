using Integration.Application.Services;
using Mapster;
using Request.Application.Services;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;
using Shared.CQRS;
using Shared.Exceptions;
using Shared.Time;

namespace Integration.Application.Features.AppraisalRequests.CreateRequest;

public class CreateRequestCommandHandler(
    ICreateRequestService createRequestService,
    IRequestDocumentValidator validator,
    IAppraisalLookupService appraisalLookup,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<CreateRequestCommand, Guid>
{
    // Purposes that continue a prior appraisal (Progressive/CI "06"/"11", Appeal "12"). Mirrors
    // Request.PriorAppraisalSubmissionGuard's set (internal there); only these purposes need — and
    // strictly require — the prior appraisal, so number resolution is scoped to them to avoid
    // 400-ing other purposes that may carry a PrevAppraisalNumber for reference only.
    private static readonly HashSet<string> PriorAppraisalRequiredPurposes =
        new(StringComparer.Ordinal) { "06", "11", "12" };

    // Cross-module string contract for AppraisalStatus.Completed (mirrors PriorAppraisalSubmissionGuard).
    private const string CompletedStatus = "Completed";

    public async Task<Guid> Handle(
        CreateRequestCommand command,
        CancellationToken cancellationToken)
    {
        // External callers reference the prior appraisal by its number (SurveyNo), not our internal
        // GUID. Resolve it to PrevAppraisalId so the downstream PriorAppraisalSubmissionGuard (which
        // requires the GUID for these purposes) is satisfied, and reject early — naming the number —
        // when it is missing or not yet Completed. An explicitly supplied GUID wins.
        if (PriorAppraisalRequiredPurposes.Contains(command.Purpose) &&
            command.Detail is { PrevAppraisalId: null, PrevAppraisalNumber: { Length: > 0 } prevNumber })
        {
            var prior = await appraisalLookup.ResolvePriorAppraisalByNumberAsync(prevNumber, cancellationToken);
            if (prior is null)
                throw new BadRequestException($"No appraisal found for AppraisalNumber '{prevNumber}'.");
            if (!string.Equals(prior.Status, CompletedStatus, StringComparison.Ordinal))
                throw new BadRequestException(
                    $"The prior appraisal '{prevNumber}' must be completed before this request can be submitted.");

            command = command with { Detail = command.Detail with { PrevAppraisalId = prior.Id } };
        }

        var input = new DocumentValidationInput(
            command.Purpose,
            (command.Documents ?? new List<RequestDocumentDto>())
            .Where(d => d.DocumentId.HasValue)
            .Select(d => d.DocumentType)
            .ToList(),
            (command.Titles ?? new List<RequestTitleDto>())
            .Select(t => new TitleDocumentInput(
                t.CollateralType,
                (t.Documents ?? new List<RequestTitleDocumentDto>())
                .Where(d => d.DocumentId.HasValue && !string.IsNullOrWhiteSpace(d.DocumentType))
                .Select(d => d.DocumentType!)
                .ToList()))
            .ToList());

        await validator.ValidateAsync(input, cancellationToken);

        var createRequestData = command.Adapt<CreateRequestData>();
        var (request, _) = await createRequestService.CreateAndSubmitRequestAsync(
            createRequestData,
            dateTimeProvider.Now,
            command.ExternalCaseKey,
            cancellationToken);

        return request.Id;
    }
}
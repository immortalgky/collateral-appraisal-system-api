using Request.Contracts.Requests.Dtos;

namespace Appraisal.Application.Services;

/// <summary>
/// Service for creating appraisals from request submissions.
/// Handles the business logic for appraisal creation workflow.
/// </summary>
public interface IAppraisalCreationService
{
    /// <summary>
    /// Creates an appraisal from a submitted request with its titles.
    /// </summary>
    /// <param name="requestId">The ID of the request</param>
    /// <param name="requestTitles">List of request titles to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the created appraisal, or existing appraisal ID if already exists</returns>
    Task<Guid> CreateAppraisalFromRequest(
        Guid requestId,
        List<RequestTitleDto> requestTitles,
        CancellationToken cancellationToken = default);
}

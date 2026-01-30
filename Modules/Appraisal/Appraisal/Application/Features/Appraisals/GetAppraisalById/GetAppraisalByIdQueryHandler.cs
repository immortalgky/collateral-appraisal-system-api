using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

/// <summary>
/// Handler for getting an Appraisal by ID
/// </summary>
public class GetAppraisalByIdQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetAppraisalByIdQuery, GetAppraisalByIdResult>
{
    public async Task<GetAppraisalByIdResult> Handle(
        GetAppraisalByIdQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(query.Id, cancellationToken);

        if (appraisal is null)
            throw new AppraisalNotFoundException(query.Id);

        return new GetAppraisalByIdResult
        {
            Id = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            RequestId = appraisal.RequestId,
            Status = appraisal.Status.ToString(),
            AppraisalType = appraisal.AppraisalType,
            Priority = appraisal.Priority,
            SLADays = appraisal.SLADays,
            SLADueDate = appraisal.SLADueDate,
            SLAStatus = appraisal.SLAStatus,
            ActualDaysToComplete = appraisal.ActualDaysToComplete,
            IsWithinSLA = appraisal.IsWithinSLA,
            PropertyCount = appraisal.Properties.Count,
            GroupCount = appraisal.Groups.Count,
            AssignmentCount = appraisal.Assignments.Count,
            CreatedOn = appraisal.CreatedOn,
            CreatedBy = appraisal.CreatedBy,
            UpdatedOn = appraisal.UpdatedOn,
            UpdatedBy = appraisal.UpdatedBy
        };
    }
}
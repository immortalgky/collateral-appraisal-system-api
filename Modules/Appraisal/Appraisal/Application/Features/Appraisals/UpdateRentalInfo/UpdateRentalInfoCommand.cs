using Appraisal.Application.Configurations;
using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.UpdateRentalInfo;

public record UpdateRentalInfoCommand(
    Guid AppraisalId,
    Guid PropertyId,
    int? NumberOfYears = null,
    DateTime? FirstYearStartDate = null,
    decimal? ContractRentalFeePerYear = null,
    decimal? UpFrontTotalAmount = null,
    string? GrowthRateType = null,
    decimal? GrowthRatePercent = null,
    int? GrowthIntervalYears = null,
    List<UpFrontEntryData>? UpFrontEntries = null,
    List<GrowthPeriodEntryData>? GrowthPeriodEntries = null,
    List<RentalScheduleEntryData>? ScheduleEntries = null,
    List<RentalScheduleOverrideData>? ScheduleOverrides = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;

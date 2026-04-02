namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Rental fee schedule information for a lease agreement property.
/// 1:1 relationship with AppraisalProperty (PropertyType = LSL, LSB, or LS)
/// </summary>
public class RentalInfo : Entity<Guid>
{
    public Guid AppraisalPropertyId { get; private set; }

    // Schedule header
    public int NumberOfYears { get; private set; }
    public DateTime? FirstYearStartDate { get; private set; }
    public decimal ContractRentalFeePerYear { get; private set; }
    public decimal UpFrontTotalAmount { get; private set; }

    // Growth rate configuration
    public string? GrowthRateType { get; private set; } // "Property" or "Period"
    public decimal GrowthRatePercent { get; private set; }
    public int GrowthIntervalYears { get; private set; }

    // Child collections
    private readonly List<RentalUpFrontEntry> _upFrontEntries = [];
    public IReadOnlyList<RentalUpFrontEntry> UpFrontEntries => _upFrontEntries.AsReadOnly();

    private readonly List<RentalGrowthPeriodEntry> _growthPeriodEntries = [];
    public IReadOnlyList<RentalGrowthPeriodEntry> GrowthPeriodEntries => _growthPeriodEntries.AsReadOnly();

    private readonly List<RentalScheduleEntry> _scheduleEntries = [];
    public IReadOnlyList<RentalScheduleEntry> ScheduleEntries => _scheduleEntries.AsReadOnly();

    private readonly List<RentalScheduleOverride> _scheduleOverrides = [];
    public IReadOnlyList<RentalScheduleOverride> ScheduleOverrides => _scheduleOverrides.AsReadOnly();

    private RentalInfo()
    {
    }

    public static RentalInfo Create(Guid appraisalPropertyId)
    {
        return new RentalInfo
        {
            AppraisalPropertyId = appraisalPropertyId,
        };
    }

    public static RentalInfo CopyFrom(RentalInfo source, Guid newPropertyId)
    {
        var copy = new RentalInfo
        {
            AppraisalPropertyId = newPropertyId,
            NumberOfYears = source.NumberOfYears,
            FirstYearStartDate = source.FirstYearStartDate,
            ContractRentalFeePerYear = source.ContractRentalFeePerYear,
            UpFrontTotalAmount = source.UpFrontTotalAmount,
            GrowthRateType = source.GrowthRateType,
            GrowthRatePercent = source.GrowthRatePercent,
            GrowthIntervalYears = source.GrowthIntervalYears
        };

        foreach (var entry in source.UpFrontEntries)
        {
            copy._upFrontEntries.Add(RentalUpFrontEntry.Create(
                copy.Id, entry.AtYear, entry.UpFrontAmount));
        }

        foreach (var entry in source.GrowthPeriodEntries)
        {
            copy._growthPeriodEntries.Add(RentalGrowthPeriodEntry.Create(
                copy.Id, entry.FromYear, entry.ToYear,
                entry.GrowthRate, entry.GrowthAmount, entry.TotalAmount));
        }

        foreach (var entry in source.ScheduleEntries)
        {
            copy._scheduleEntries.Add(RentalScheduleEntry.Create(
                copy.Id, entry.Year, entry.ContractStart, entry.ContractEnd,
                entry.UpFront, entry.ContractRentalFee, entry.TotalAmount,
                entry.ContractRentalFeeGrowthRatePercent));
        }

        foreach (var entry in source.ScheduleOverrides)
        {
            copy._scheduleOverrides.Add(RentalScheduleOverride.Create(
                copy.Id, entry.Year, entry.UpFront, entry.ContractRentalFee));
        }

        return copy;
    }

    public void Update(
        int? numberOfYears = null,
        DateTime? firstYearStartDate = null,
        decimal? contractRentalFeePerYear = null,
        decimal? upFrontTotalAmount = null,
        string? growthRateType = null,
        decimal? growthRatePercent = null,
        int? growthIntervalYears = null)
    {
        if (numberOfYears.HasValue) NumberOfYears = numberOfYears.Value;
        if (firstYearStartDate.HasValue) FirstYearStartDate = firstYearStartDate.Value;
        if (contractRentalFeePerYear.HasValue) ContractRentalFeePerYear = contractRentalFeePerYear.Value;
        if (upFrontTotalAmount.HasValue) UpFrontTotalAmount = upFrontTotalAmount.Value;
        if (growthRateType is not null) GrowthRateType = growthRateType;
        if (growthRatePercent.HasValue) GrowthRatePercent = growthRatePercent.Value;
        if (growthIntervalYears.HasValue) GrowthIntervalYears = growthIntervalYears.Value;
    }

    public void AddUpFrontEntry(int atYear, decimal upFrontAmount)
    {
        _upFrontEntries.Add(RentalUpFrontEntry.Create(Id, atYear, upFrontAmount));
    }

    public void ClearUpFrontEntries()
    {
        _upFrontEntries.Clear();
    }

    public void AddGrowthPeriodEntry(int fromYear, int toYear, decimal growthRate, decimal growthAmount, decimal totalAmount)
    {
        _growthPeriodEntries.Add(RentalGrowthPeriodEntry.Create(
            Id, fromYear, toYear, growthRate, growthAmount, totalAmount));
    }

    public void ClearGrowthPeriodEntries()
    {
        _growthPeriodEntries.Clear();
    }

    public void AddScheduleEntry(int year, DateTime contractStart, DateTime contractEnd,
        decimal upFront, decimal contractRentalFee, decimal totalAmount, decimal growthRatePercent)
    {
        _scheduleEntries.Add(RentalScheduleEntry.Create(
            Id, year, contractStart, contractEnd, upFront, contractRentalFee, totalAmount, growthRatePercent));
    }

    public void ClearScheduleEntries()
    {
        _scheduleEntries.Clear();
    }

    public void SetScheduleOverride(int year, decimal? upFront, decimal? contractRentalFee)
    {
        _scheduleOverrides.Add(RentalScheduleOverride.Create(Id, year, upFront, contractRentalFee));
    }

    public void ClearScheduleOverrides()
    {
        _scheduleOverrides.Clear();
    }
}

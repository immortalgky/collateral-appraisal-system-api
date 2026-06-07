namespace Reporting.Application.OperationalReports.Rcas010;

/// <summary>
/// One aggregated row of RCAS010 (bank-absorbed fee expense), grouped by channel + assign type.
/// </summary>
public sealed record Rcas010Row(
    string? Channel,
    string? AssignType,
    int BookCount,
    decimal? TotalFee,
    int CustomerPaidCount,
    decimal? CustomerPaidFee,
    int BankAbsorbCount,
    decimal? BankAbsorbFee);

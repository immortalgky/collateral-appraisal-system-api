namespace Reporting.Application.OperationalReports.Rcas010;

/// <summary>
/// RCAS010 is a single SUMMARY row (FSD Table 28): Internal vs External appraisal, each split into
/// Total / Customer-Paid / Bank-Absorb by book count + fee, plus a Grand Total. The whole report is
/// one row recomputed for whatever filters (channel, department, AO, status, fee type, company,
/// create-date) are applied. Column order here MUST mirror the SELECT in Rcas010Report.Build
/// (positional Dapper record).
/// </summary>
public sealed record Rcas010Row(
    int InternalBookCount,
    decimal? InternalTotalFee,
    int InternalCustomerPaidCount,
    decimal? InternalCustomerPaidFee,
    int InternalBankAbsorbCount,
    decimal? InternalBankAbsorbFee,
    int ExternalBookCount,
    decimal? ExternalTotalFee,
    int ExternalCustomerPaidCount,
    decimal? ExternalCustomerPaidFee,
    int ExternalBankAbsorbCount,
    decimal? ExternalBankAbsorbFee,
    int GrandTotalCount,
    decimal? GrandTotalFee);

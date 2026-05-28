namespace Common.Domain.Logs;

/// <summary>
/// Plain POCO — intentionally does NOT implement any audit/entity marker interface.
/// EF owns the schema via LogConfiguration; Serilog writes rows directly via ADO.NET.
/// Column names and types must stay in sync with the MSSqlServer sink config.
/// </summary>
public class Log
{
    public long Id { get; set; }
    public DateTime TimeStamp { get; set; }
    public string? Level { get; set; }
    public string? Message { get; set; }
    public string? Exception { get; set; }
    public string? Properties { get; set; }
    public string? CorrelationId { get; set; }
    public string? EntityId { get; set; }
    public string? AppraisalId { get; set; }
    public string? RequestId { get; set; }
    public string? WorkflowInstanceId { get; set; }
    public string? CollateralId { get; set; }
    public string? DocumentId { get; set; }
    public string? MachineName { get; set; }
}

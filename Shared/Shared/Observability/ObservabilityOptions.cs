namespace Shared.Observability;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool Enabled { get; set; } = true;
    public string ServiceName { get; set; } = "CollateralAppraisalSystem";
    public string ServiceVersion { get; set; } = "1.0.0";
    public TracingOptions Tracing { get; set; } = new();
}

public class TracingOptions
{
    public string? OtlpEndpoint { get; set; }
}

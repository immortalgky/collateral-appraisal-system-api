# Workflow Module Observability Enhancement Plan

## Overview
Implementation of comprehensive observability enhancements for the Workflow module in .NET 9.0 modular monolith application using OpenTelemetry for tracing, metrics, and enhanced structured logging with Serilog.

## Current State Analysis
- **Location**: `/Modules/Workflow/Workflow/`
- **Current Logging**: Basic ILogger usage throughout (WorkflowEngine.cs, services)
- **Infrastructure**: Serilog configured at API level in Program.cs
- **Resilience**: Polly patterns implemented but no observability
- **Gaps**: No metrics, no distributed tracing, minimal structured logging

## Implementation Plan

### Phase 1: OpenTelemetry Infrastructure Setup
- [ ] **1.1** Add OpenTelemetry NuGet packages to Workflow.csproj:
  - OpenTelemetry (1.7.0)
  - OpenTelemetry.Extensions.Hosting (1.7.0)
  - OpenTelemetry.Instrumentation.AspNetCore (1.7.1)
  - OpenTelemetry.Instrumentation.Http (1.7.1)
  - OpenTelemetry.Exporter.Console (1.7.0)
  - OpenTelemetry.Exporter.OpenTelemetryProtocol (1.7.0)

- [ ] **1.2** Create folder structure:
  - `/Workflow/Telemetry/`
  - `/Workflow/Telemetry/Configuration/`
  - `/Workflow/Telemetry/Logging/`
  - `/Workflow/Telemetry/Metrics/`
  - `/Workflow/Telemetry/Tracing/`

- [ ] **1.3** Create `WorkflowTelemetryConfiguration.cs` with:
  - OpenTelemetry service registration
  - OTLP exporter configuration
  - Console exporter for development
  - Resource configuration

- [ ] **1.4** Update `WorkflowModule.cs`:
  - Register telemetry services
  - Configure OpenTelemetry pipeline
  - Integration with existing DI container

### Phase 2: Structured Logging Enhancement
- [ ] **2.1** Create `WorkflowTelemetryConstants.cs`:
  - Semantic logging property names
  - Log message templates
  - Activity source names
  - Meter names

- [ ] **2.2** Create `IWorkflowLogger` interface and implementation:
  - Standardized logging methods
  - Correlation context propagation
  - Structured logging enforcement
  - Scoped logging patterns

- [ ] **2.3** Create `WorkflowLoggingExtensions.cs`:
  - Extension methods for common logging scenarios
  - Workflow lifecycle logging
  - Activity execution logging
  - Error and exception logging

- [ ] **2.4** Update `WorkflowEngine.cs`:
  - Replace ILogger<WorkflowEngine> with IWorkflowLogger
  - Add structured properties to all log statements
  - Implement correlation ID propagation
  - Add performance logging

### Phase 3: Metrics Implementation
- [ ] **3.1** Create `WorkflowMetrics.cs`:
  - Define Meter for workflow operations
  - Counter metrics:
    - `workflows_started_total`
    - `workflows_completed_total`
    - `workflows_failed_total`
    - `workflows_suspended_total`
    - `activities_executed_total`
    - `activities_failed_total`
  - Histogram metrics:
    - `workflow_execution_duration_seconds`
    - `activity_execution_duration_seconds`
    - `bookmark_processing_duration_seconds`
  - Gauge metrics:
    - `active_workflows_count`
    - `pending_activities_count`
    - `suspended_workflows_count`

- [ ] **3.2** Create `IWorkflowMetricsCollector` interface and implementation:
  - Encapsulate metrics collection logic
  - Provide typed methods for each metric
  - Handle metric dimensions and tags

- [ ] **3.3** Instrument `WorkflowEngine.cs`:
  - Add workflow lifecycle metrics
  - Track activity execution times
  - Monitor bookmark operations
  - Record error rates and counts

- [ ] **3.4** Instrument additional services:
  - `WorkflowOrchestrator.cs`
  - `WorkflowBookmarkService.cs`
  - `WorkflowResilienceService.cs`

### Phase 4: Distributed Tracing
- [ ] **4.1** Create `WorkflowActivitySource.cs`:
  - Define ActivitySource for workflow operations
  - Create semantic conventions for spans
  - Define span names and attributes

- [ ] **4.2** Create `WorkflowTracing.cs` utility class:
  - Helper methods for creating spans
  - Baggage management
  - Correlation propagation
  - Error handling for spans

- [ ] **4.3** Instrument `WorkflowEngine.ExecuteWorkflowAsync`:
  - Root span for workflow execution
  - Child spans for each activity
  - Add workflow context to baggage
  - Track execution flow

- [ ] **4.4** Instrument activity execution:
  - Spans for individual activities
  - External service calls tracing
  - Database operation spans
  - Resilience operation tracing

### Phase 5: Integration and Configuration
- [ ] **5.1** Update `appsettings.Development.json`:
  - Add OpenTelemetry configuration section
  - Configure OTLP exporters
  - Set sampling rates
  - Configure console exporter

- [ ] **5.2** Create `WorkflowObservabilityOptions.cs`:
  - Configuration model for telemetry settings
  - Exporter configurations
  - Sampling configurations
  - Feature toggles

- [ ] **5.3** Integration testing:
  - Verify metrics collection
  - Test distributed tracing
  - Validate log correlation
  - Performance impact assessment

### Phase 6: Documentation and Testing
- [ ] **6.1** Update XML documentation:
  - Document new interfaces
  - Add telemetry usage examples
  - Performance considerations

- [ ] **6.2** Create unit tests:
  - Test metrics collection
  - Validate logging behavior
  - Mock tracing scenarios

- [ ] **6.3** Integration tests:
  - End-to-end observability
  - External system integration
  - Performance benchmarks

## File Structure After Implementation

```
Modules/Workflow/Workflow/
├── Telemetry/
│   ├── Configuration/
│   │   ├── WorkflowTelemetryConfiguration.cs
│   │   └── WorkflowObservabilityOptions.cs
│   ├── Logging/
│   │   ├── IWorkflowLogger.cs
│   │   ├── WorkflowLogger.cs
│   │   └── WorkflowLoggingExtensions.cs
│   ├── Metrics/
│   │   ├── WorkflowMetrics.cs
│   │   └── IWorkflowMetricsCollector.cs
│   ├── Tracing/
│   │   ├── WorkflowActivitySource.cs
│   │   └── WorkflowTracing.cs
│   └── WorkflowTelemetryConstants.cs
├── Workflow/
│   ├── Engine/
│   │   └── WorkflowEngine.cs (updated)
│   └── Services/
│       ├── WorkflowOrchestrator.cs (updated)
│       ├── WorkflowBookmarkService.cs (updated)
│       └── WorkflowResilienceService.cs (updated)
├── WorkflowModule.cs (updated)
└── Workflow.csproj (updated)
```

## Configuration Changes

### appsettings.Development.json additions:
```json
{
  "OpenTelemetry": {
    "ServiceName": "Collateral.Appraisal.Workflow",
    "ServiceVersion": "1.0.0",
    "Tracing": {
      "Enabled": true,
      "ConsoleExporter": true,
      "OtlpExporter": {
        "Enabled": false,
        "Endpoint": "http://localhost:4317"
      },
      "Sampling": {
        "Type": "TraceIdRatioBased",
        "Ratio": 1.0
      }
    },
    "Metrics": {
      "Enabled": true,
      "ConsoleExporter": true,
      "OtlpExporter": {
        "Enabled": false,
        "Endpoint": "http://localhost:4317"
      },
      "ExportInterval": 5000
    },
    "Logging": {
      "IncludeScopes": true,
      "IncludeFormattedMessage": true
    }
  }
}
```

## Success Criteria
- [ ] All workflow operations emit structured logs with correlation IDs
- [ ] Metrics collected for workflow lifecycle and performance
- [ ] Distributed tracing spans created for workflow execution
- [ ] Integration with existing Serilog configuration maintained
- [ ] Performance overhead < 5% for typical workflow operations
- [ ] Backward compatibility maintained
- [ ] No breaking changes to existing APIs

## Risk Mitigation
- **Performance Impact**: Use sampling and feature toggles
- **Memory Usage**: Implement proper resource cleanup
- **Backward Compatibility**: Maintain existing ILogger interfaces
- **Configuration Complexity**: Provide sensible defaults
- **External Dependencies**: Make exporters optional

## Review Checklist
- [ ] Code follows existing patterns and conventions
- [ ] All public APIs have XML documentation
- [ ] Unit tests cover new functionality
- [ ] Integration tests validate end-to-end scenarios
- [ ] Performance impact assessed
- [ ] Security review completed (no sensitive data in telemetry)
- [ ] Configuration validated in development environment
# Phase 2: Structured Logging Enhancement - TODO

## Overview
Implement structured logging enhancement for the Workflow module with high-performance logging patterns, correlation context, and semantic properties.

## Tasks

### 1. Create IWorkflowLogger Interface
- [ ] Create `IWorkflowLogger` interface in Telemetry folder
- [ ] Define structured logging methods for workflow lifecycle events (started, completed, failed, suspended)
- [ ] Define methods for activity execution events
- [ ] Define methods for bookmark operations
- [ ] Use semantic logging with structured parameters

### 2. Create WorkflowLogger Implementation
- [ ] Implement `IWorkflowLogger` interface
- [ ] Use `ILogger<WorkflowLogger>` internally for actual logging
- [ ] Add BeginScope for correlation context
- [ ] Use structured logging with semantic properties from constants
- [ ] Include performance logging with durations

### 3. Create WorkflowLoggingExtensions
- [ ] Extension methods for ILogger to provide structured logging
- [ ] Use high-performance logging with `LoggerMessage.Define`
- [ ] Include all workflow operation types
- [ ] Support correlation and timing information

### 4. Update WorkflowModule.cs
- [ ] Register `IWorkflowLogger` in DI container
- [ ] Add logging configuration options if needed

### 5. Update WorkflowEngine.cs (Partial Update)
- [ ] Replace some existing logging calls with structured equivalents
- [ ] Focus on key methods like `StartWorkflowAsync` and `ResumeWorkflowAsync`
- [ ] Add correlation scopes using workflow instance ID
- [ ] Show examples of the enhanced logging pattern
- [ ] Maintain backward compatibility

## Requirements
- Use high-performance logging patterns (`LoggerMessage.Define`)
- Include correlation context with workflow and activity IDs
- Add timing information for operations
- Maintain backward compatibility
- Follow existing error handling patterns
- Use the telemetry constants defined in Phase 1

## Definition of Done
- All telemetry interfaces and implementations are created
- WorkflowModule is updated with proper DI registration
- WorkflowEngine shows enhanced logging examples
- All code follows security best practices
- Simple, minimal changes with maximum impact
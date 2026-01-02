# Phase 4: Distributed Tracing Implementation Plan

## Overview
Implement distributed tracing for the Workflow module using System.Diagnostics.Activity API and OpenTelemetry semantic conventions.

## Tasks

### 1. Create IWorkflowTracing interface ✅
- [x] Define methods for creating and managing workflow spans
- [x] Include methods for workflow operation spans (start, execute, complete, suspend, resume, cancel)
- [x] Include methods for activity execution spans
- [x] Include methods for external call spans
- [x] Include methods for database operation spans
- [x] Include baggage management for workflow context propagation

### 2. Create WorkflowTracing implementation ✅
- [x] Use System.Diagnostics.Activity API with WorkflowTelemetryConstants.ActivitySource
- [x] Implement span creation for workflow operations with proper naming
- [x] Add semantic attributes following OpenTelemetry conventions
- [x] Include baggage for workflow correlation context
- [x] Implement IDisposable pattern for automatic span completion
- [x] Handle error recording and exception tagging

### 3. Create WorkflowTracingExtensions.cs ✅
- [x] Extension methods for automatic span management
- [x] Using statement patterns for span lifecycles
- [x] Helper methods for adding semantic attributes
- [x] Convenience methods for error handling in spans

### 4. Update WorkflowModule.cs ✅
- [x] Register IWorkflowTracing in DI container
- [x] Add to Phase 4 section with proper comment

### 5. Update WorkflowEngine.cs (selective integration) ✅
- [x] Add IWorkflowTracing dependency injection
- [x] Create spans for key operations:
  - StartWorkflowAsync: create root workflow span ✅
  - ExecuteWorkflowAsync: create execution span ✅
  - ExecuteSingleActivityAsync: create activity execution span ✅
  - Database operations: create database spans ✅ (WriteWorkflowStartedEventAsync example)
- [x] Add semantic attributes to spans (workflow_id, activity_id, workflow_type, etc.)
- [x] Propagate baggage with workflow context
- [x] Handle span completion and error recording

## Technical Requirements
- Use System.Diagnostics.Activity API (standard .NET tracing)
- Follow OpenTelemetry semantic conventions for span names and attributes
- Include proper baggage for workflow context propagation
- Integrate with existing logging and metrics
- Make tracing optional with feature toggles
- Maintain backward compatibility
- Use the telemetry constants from Phase 1

## Review Section ✅

### Implementation Summary
Successfully implemented Phase 4: Distributed Tracing for the Workflow module with comprehensive span management and OpenTelemetry-compatible tracing.

### Files Created
1. **`IWorkflowTracing.cs`** - Interface defining tracing contract with methods for:
   - Workflow operation spans (start, execute, suspend, resume, etc.)
   - Activity execution spans 
   - External call spans
   - Database operation spans
   - Bookmark operation spans
   - Baggage management for context propagation

2. **`WorkflowTracing.cs`** - Implementation using System.Diagnostics.Activity API:
   - Creates spans using WorkflowTelemetryConstants.ActivitySource
   - Follows OpenTelemetry semantic conventions
   - Automatic baggage propagation
   - Comprehensive error handling and exception recording

3. **`WorkflowTracingExtensions.cs`** - Extension methods providing:
   - Fluent API for span management
   - Automatic span lifecycle with 'using' patterns
   - Helper methods for semantic attributes
   - Convenience methods for timing, milestones, and HTTP responses

### Files Modified
1. **`WorkflowModule.cs`** - Added IWorkflowTracing service registration in Phase 4 section
2. **`WorkflowEngine.cs`** - Integrated distributed tracing in key methods:
   - **StartWorkflowAsync**: Root workflow span with correlation propagation
   - **ExecuteWorkflowAsync**: Execution span with activity context
   - **ExecuteSingleActivityAsync**: Activity execution span with unique execution ID
   - **WriteWorkflowStartedEventAsync**: Database operation span example

### Key Features Implemented
- **System.Diagnostics.Activity API**: Uses standard .NET distributed tracing
- **OpenTelemetry Compatibility**: Follows semantic conventions for span names and attributes
- **Baggage Propagation**: Automatically propagates workflow context across service boundaries
- **Exception Handling**: Records exceptions with structured data and stack traces
- **Fluent API**: Easy-to-use extension methods for common tracing patterns
- **Performance-Aware**: Minimal overhead with IDisposable patterns

### Integration Examples
- **Root Spans**: `StartWorkflowAsync` creates root workflow span with correlation ID
- **Child Spans**: `ExecuteWorkflowAsync` creates child execution spans
- **Activity Spans**: `ExecuteSingleActivityAsync` creates activity-specific spans
- **Database Spans**: `WriteWorkflowStartedEventAsync` demonstrates database operation tracing
- **Error Handling**: Exception recording with span status and structured attributes

### OpenTelemetry Features
- **Semantic Attributes**: workflow.instance.id, workflow.definition.id, workflow.activity.type, etc.
- **Activity Names**: workflow.start, workflow.execute, workflow.activity.execute, etc.
- **Baggage**: correlation_id, workflow_instance_id, workflow_definition_id propagation
- **Status Codes**: ActivityStatusCode.Ok/Error with descriptive messages
- **Events**: Exception events, milestones, timing information

### Benefits
1. **End-to-End Visibility**: Track workflow execution across distributed systems
2. **Performance Monitoring**: Identify bottlenecks in workflow and activity execution
3. **Error Correlation**: Connect failures to specific workflow instances and activities
4. **Service Dependencies**: Trace external calls and database operations
5. **Context Propagation**: Maintain workflow context across service boundaries

The implementation provides a solid foundation for distributed tracing that integrates seamlessly with existing logging and metrics, enabling comprehensive observability for workflow operations.
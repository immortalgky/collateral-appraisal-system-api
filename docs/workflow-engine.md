# Workflow Engine Documentation

This document provides comprehensive documentation for the Workflow Engine system, including architecture diagrams, process flows, and the key EF Core fix that was implemented.

## Table of Contents
1. [System Architecture](#system-architecture)
2. [StartWorkflow Process Flow](#startworkflow-process-flow)
3. [ResumeWorkflow Process Flow](#resumeworkflow-process-flow)
4. [Entity Relationship Diagram](#entity-relationship-diagram)
5. [Activity Execution Lifecycle](#activity-execution-lifecycle)
6. [Key Fix: EF Core State Management](#key-fix-ef-core-state-management)
7. [Implementation Notes](#implementation-notes)

## System Architecture

The workflow engine follows a clean architecture pattern with clear separation of concerns:

```mermaid
graph TB
    %% External Clients
    Client[Client Application] --> WE[WorkflowEngine]
    
    %% Core Components
    WE --> WDR[IWorkflowDefinitionRepository]
    WE --> WIR[IWorkflowInstanceRepository] 
    WE --> WAF[IWorkflowActivityFactory]
    WE --> PE[IPublishEndpoint - MassTransit]
    
    %% Repositories
    WDR --> DB[(Database)]
    WIR --> DB
    
    %% Activity Factory
    WAF --> SA[StartActivity]
    WAF --> EA[EndActivity] 
    WAF --> DA[DecisionActivity]
    WAF --> Custom[Custom Activities]
    
    %% Domain Models
    WE --> WI[WorkflowInstance]
    WE --> WAE[WorkflowActivityExecution]
    WE --> WS[WorkflowSchema]
    
    %% Events
    PE --> Events[Workflow Events]
    Events --> WSE[WorkflowStarted]
    Events --> WACE[WorkflowActivityCompleted]
    Events --> WCE[WorkflowCancelled]
    
    %% Styling
    classDef engine fill:#e1f5fe
    classDef repository fill:#f3e5f5
    classDef activity fill:#e8f5e8
    classDef model fill:#fff3e0
    classDef event fill:#fce4ec
    
    class WE engine
    class WDR,WIR repository
    class SA,EA,DA,Custom activity
    class WI,WAE,WS model
    class WSE,WACE,WCE event
```

### Key Components

- **WorkflowEngine**: Central orchestrator that manages workflow execution
- **Repositories**: Data access layer following repository pattern
- **ActivityFactory**: Creates appropriate activity instances based on type
- **Domain Models**: Rich domain entities following DDD principles
- **Event Publishing**: Integration with MassTransit for workflow events

## StartWorkflow Process Flow

This diagram shows how a new workflow is initiated:

```mermaid
sequenceDiagram
    participant C as Client
    participant WE as WorkflowEngine
    participant WDR as WorkflowDefinitionRepository
    participant WIR as WorkflowInstanceRepository
    participant WAF as ActivityFactory
    participant PE as PublishEndpoint
    participant DB as Database
    
    C->>WE: StartWorkflowAsync(definitionId, name, startedBy)
    
    %% Load workflow definition
    WE->>WDR: GetByIdAsync(definitionId)
    WDR->>DB: SELECT WorkflowDefinition
    DB-->>WDR: WorkflowDefinition
    WDR-->>WE: WorkflowDefinition
    
    %% Deserialize and validate
    WE->>WE: JsonSerializer.Deserialize<WorkflowSchema>
    WE->>WE: Find start activity
    
    %% Create workflow instance
    WE->>WE: WorkflowInstance.Create()
    WE->>WE: SetCurrentActivity(startActivity.Id)
    
    %% Execute first activity
    WE->>WE: ExecuteActivityAsync()
    
    rect rgb(240, 248, 255)
        Note over WE: ExecuteActivityAsync Loop
        WE->>WE: WorkflowActivityExecution.Create() [No Guid set]
        WE->>WE: workflowInstance.AddActivityExecution()
        WE->>WE: activityExecution.Start()
        WE->>WAF: CreateActivity(activityType)
        WAF-->>WE: Activity instance
        WE->>WE: activity.ExecuteAsync(context)
        WE->>WE: Process result & determine next activity
        WE->>WE: Queue next activity if exists
    end
    
    %% Persist to database
    WE->>WIR: AddAsync(workflowInstance)
    WE->>WIR: SaveChangesAsync()
    WIR->>DB: INSERT WorkflowInstance + ActivityExecutions [EF generates Guids]
    DB-->>WIR: Success
    WIR-->>WE: Success
    
    %% Publish event
    WE->>PE: Publish(WorkflowStarted)
    PE-->>WE: Event published
    
    WE-->>C: WorkflowInstance
```

### Key Points
- Entire workflow instance graph is new, so EF Core correctly marks all entities as `Added`
- Activities are executed in a queue-based system for sequential processing
- Events are published for external system integration

## ResumeWorkflow Process Flow

This diagram shows how an existing workflow is resumed after an external activity completion:

```mermaid
sequenceDiagram
    participant C as Client
    participant WE as WorkflowEngine
    participant WDR as WorkflowDefinitionRepository
    participant WIR as WorkflowInstanceRepository
    participant PE as PublishEndpoint
    participant DB as Database
    
    C->>WE: ResumeWorkflowAsync(instanceId, activityId, outputData, completedBy)
    
    %% Load workflow instance with tracking
    WE->>WIR: GetByIdAsync(instanceId) [WITH TRACKING]
    WIR->>DB: SELECT WorkflowInstance + ActivityExecutions
    DB-->>WIR: Tracked entities
    WIR-->>WE: WorkflowInstance (tracked)
    
    %% Load workflow definition
    WE->>WDR: GetByIdAsync(workflowDefinitionId)
    WDR-->>WE: WorkflowDefinition
    WE->>WE: JsonSerializer.Deserialize<WorkflowSchema>
    
    %% Find and complete current activity
    WE->>WE: Find activityExecution (InProgress)
    WE->>WE: activityExecution.Complete(completedBy, outputData)
    
    Note over WE: EF Core detects changes in tracked entity
    
    %% Update workflow variables
    WE->>WE: workflowInstance.UpdateVariables(variableUpdates)
    
    %% Determine next activity
    WE->>WE: DetermineNextActivityAsync()
    
    alt Has next activity
        WE->>WE: SetCurrentActivity(nextActivity.Id)
        WE->>WE: ExecuteActivityAsync()
        
        rect rgb(255, 240, 240)
            Note over WE: Critical: New ActivityExecution Creation
            WE->>WE: WorkflowActivityExecution.Create() [No Guid - fixed!]
            WE->>WE: workflowInstance.AddActivityExecution() [Added to tracked parent]
            Note over WE: EF Core now correctly marks as Added
            WE->>WE: activityExecution.Start()
        end
        
    else Workflow complete
        WE->>WE: workflowInstance.UpdateStatus(Completed)
    end
    
    %% Save changes (NO UpdateAsync call - just SaveChanges)
    WE->>WIR: SaveChangesAsync() [No UpdateAsync!]
    
    rect rgb(240, 255, 240)
        Note over WIR,DB: EF Core Change Tracking
        WIR->>DB: UPDATE WorkflowInstance (Modified)
        WIR->>DB: UPDATE existing ActivityExecution (Modified)  
        WIR->>DB: INSERT new ActivityExecutions (Added)
        DB-->>WIR: Success - correct row counts
    end
    
    WIR-->>WE: Success
    
    %% Publish event
    WE->>PE: Publish(WorkflowActivityCompleted)
    PE-->>WE: Event published
    
    WE-->>C: WorkflowInstance (updated)
```

### Key Points
- Uses EF Core change tracking instead of explicit `Update()` calls
- Mixed entity states: existing entities are Modified, new entities are Added
- The fix ensures new ActivityExecutions are properly marked as Added

## Entity Relationship Diagram

```mermaid
erDiagram
    WorkflowDefinition {
        Guid Id PK
        string Name
        string Description
        int Version
        bool IsActive
        string JsonDefinition
        string Category
        DateTime CreatedOn
        string CreatedBy
        DateTime UpdatedOn
        string UpdatedBy
    }
    
    WorkflowInstance {
        Guid Id PK "EF Generated"
        Guid WorkflowDefinitionId FK
        string Name
        string CorrelationId
        string Status "Running|Completed|Failed|Cancelled"
        string CurrentActivityId
        string CurrentAssignee
        DateTime StartedOn
        DateTime CompletedOn
        string StartedBy
        string Variables "JSON"
        string ErrorMessage
        int RetryCount
    }
    
    WorkflowActivityExecution {
        Guid Id PK "EF Generated - Fixed!"
        Guid WorkflowInstanceId FK
        string ActivityId
        string ActivityName
        string ActivityType
        string Status "Pending|InProgress|Completed|Failed"
        string AssignedTo
        DateTime StartedOn
        DateTime CompletedOn
        string CompletedBy
        string InputData "JSON"
        string OutputData "JSON"
        string ErrorMessage
        string Comments
    }
    
    WorkflowDefinition ||--o{ WorkflowInstance : defines
    WorkflowInstance ||--o{ WorkflowActivityExecution : executes
    
    %% Notes on relationships
    WorkflowDefinition ||--o{ WorkflowInstance : "1 definition can have many instances"
    WorkflowInstance ||--o{ WorkflowActivityExecution : "1 instance has many activity executions"
```

### Database Design Notes
- All primary keys are Guids generated by EF Core
- JSON columns store complex data (Variables, InputData, OutputData)
- Proper foreign key relationships with cascade delete for ActivityExecutions
- Status fields use string conversion for readability

## Activity Execution Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Created : WorkflowActivityExecution.Create()
    
    Created --> Pending : Initial state
    Created : Id = Guid.Empty (EF will generate)
    Created : InputData set from workflow variables
    
    Pending --> InProgress : activityExecution.Start()
    InProgress : Status = InProgress
    InProgress : StartedOn = DateTime.UtcNow
    
    InProgress --> Completed : activityExecution.Complete()
    InProgress --> Failed : activityExecution.Fail()
    InProgress --> Skipped : activityExecution.Skip()
    InProgress --> Cancelled : Workflow cancelled
    
    Completed : Status = Completed
    Completed : CompletedOn = DateTime.UtcNow
    Completed : CompletedBy set
    Completed : OutputData populated
    Completed : Comments optional
    
    Failed : Status = Failed
    Failed : CompletedOn = DateTime.UtcNow
    Failed : ErrorMessage set
    
    Skipped : Status = Skipped
    Skipped : CompletedOn = DateTime.UtcNow
    Skipped : Comments = reason
    
    Cancelled : Status = Cancelled
    Cancelled : Workflow-level cancellation
    
    Completed --> [*]
    Failed --> [*]
    Skipped --> [*]
    Cancelled --> [*]
    
    note right of Created
        Key Fix: No Guid set here
        EF Core generates during SaveChanges
        Ensures proper Added state tracking
    end note
    
    note right of InProgress
        External activities remain here
        until ResumeWorkflow is called
        with completion data
    end note
```

### Activity States
- **Created**: Entity instantiated but not persisted
- **Pending**: Default state after creation
- **InProgress**: Activity is actively being executed
- **Completed**: Successfully finished with output data
- **Failed**: Execution failed with error message
- **Skipped**: Activity was bypassed with reason
- **Cancelled**: Workflow-level cancellation

## Implementation Notes

### Key Design Decisions
1. **DDD Principles**: Domain entities manage their own behavior
2. **Repository Pattern**: Clean separation of data access concerns
3. **Event-Driven Architecture**: Integration through domain events
4. **EF Core Change Tracking**: Leveraged instead of manual state management

### Performance Considerations
- EF Core generates random Guids (not sequential)
- For high-volume scenarios, consider `NEWSEQUENTIALID()` configuration
- Change tracking works efficiently for mixed entity states
- Navigation property loading uses `Include()` for efficient queries

### Error Handling
- Comprehensive validation at workflow definition level
- Activity-level error handling with proper state transitions
- Transactional consistency through EF Core unit of work pattern
- Event publishing for external system integration

### Testing Strategies
- Unit tests for domain logic in entities
- Integration tests for repository implementations
- End-to-end tests for complete workflow scenarios
- Mock external dependencies (MassTransit, database)

---
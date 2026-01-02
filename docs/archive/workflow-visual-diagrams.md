# Workflow Module Visual Diagrams
## Architecture Visualizations and Flow Charts

### Table of Contents
1. [System Architecture Diagrams](#system-architecture-diagrams)
2. [Workflow Execution Flow](#workflow-execution-flow)
3. [Database Schema Diagrams](#database-schema-diagrams)
4. [Background Services Architecture](#background-services-architecture)
5. [API Integration Patterns](#api-integration-patterns)
6. [Error Handling Flows](#error-handling-flows)
7. [Configuration Hierarchy](#configuration-hierarchy)

---

## System Architecture Diagrams

### 1. High-Level System Architecture

```mermaid
graph TB
    subgraph "Client Layer"
        WebApp[Web Application]
        MobileApp[Mobile App]
        API[External API Clients]
    end
    
    subgraph "API Gateway"
        Gateway[API Gateway]
        Auth[Authentication Service]
        RateLimit[Rate Limiting]
    end
    
    subgraph "Workflow Module"
        subgraph "Presentation Layer"
            CarterEndpoints[Carter Endpoints]
            SignalRHub[SignalR Hub]
        end
        
        subgraph "Service Layer"
            WorkflowService[WorkflowService]
            ResilienceService[ResilienceService]
            FaultHandler[FaultHandler]
        end
        
        subgraph "Engine Layer"
            WorkflowEngine[WorkflowEngine]
            FlowControl[FlowControlManager]
            Lifecycle[LifecycleManager]
            StateManager[StateManager]
        end
        
        subgraph "Activity Layer"
            TaskActivity[TaskActivity]
            CustomActivities[Custom Activities]
            SystemActivities[System Activities]
        end
        
        subgraph "Background Services"
            OutboxDispatcher[OutboxDispatcherService]
            TimerService[WorkflowTimerService]
            CleanupService[WorkflowCleanupService]
        end
    end
    
    subgraph "Data Layer"
        WorkflowDB[(Workflow Database)]
        SagaDB[(Saga Database)]
    end
    
    subgraph "External Services"
        NotificationService[Notification Service]
        ValuationService[Valuation API]
        DocumentService[Document Service]
    end
    
    WebApp --> Gateway
    MobileApp --> Gateway
    API --> Gateway
    
    Gateway --> Auth
    Gateway --> RateLimit
    Gateway --> CarterEndpoints
    
    CarterEndpoints --> WorkflowService
    SignalRHub --> WorkflowService
    
    WorkflowService --> ResilienceService
    WorkflowService --> WorkflowEngine
    ResilienceService --> FaultHandler
    
    WorkflowEngine --> FlowControl
    WorkflowEngine --> Lifecycle
    WorkflowEngine --> StateManager
    
    WorkflowEngine --> TaskActivity
    WorkflowEngine --> CustomActivities
    WorkflowEngine --> SystemActivities
    
    OutboxDispatcher --> NotificationService
    TimerService --> WorkflowEngine
    CleanupService --> WorkflowDB
    
    StateManager --> WorkflowDB
    Lifecycle --> WorkflowDB
    OutboxDispatcher --> WorkflowDB
    
    CustomActivities --> ValuationService
    CustomActivities --> DocumentService
    
    style WorkflowEngine fill:#e1f5fe
    style ResilienceService fill:#f3e5f5
    style OutboxDispatcher fill:#e8f5e8
```

### 2. Workflow Service Architecture Detail

```mermaid
graph TD
    subgraph "WorkflowService (Enhanced)"
        WS_Start[StartWorkflowAsync]
        WS_Resume[ResumeWorkflowAsync]
        WS_Cancel[CancelWorkflowAsync]
        WS_Get[GetWorkflowInstanceAsync]
    end
    
    subgraph "Resilience Layer"
        RS_Retry[Retry Logic]
        RS_Timeout[Timeout Handling]
        RS_Circuit[Circuit Breaker]
        RS_Metrics[Metrics Collection]
    end
    
    subgraph "WorkflowEngine (Core)"
        WE_Execute[ExecuteWorkflowAsync]
        WE_Validate[ValidateWorkflowDefinitionAsync]
        WE_Activity[ExecuteSingleActivityAsync]
        WE_Next[DetermineNextActivityAsync]
    end
    
    subgraph "Specialized Managers"
        FlowMgr[FlowControlManager]
        LifeMgr[WorkflowLifecycleManager]
        StateMgr[WorkflowStateManager]
        PersistMgr[WorkflowPersistenceService]
    end
    
    subgraph "Event Publishing"
        EventPub[WorkflowEventPublisher]
        Outbox[WorkflowOutbox]
        SignalR[SignalR Notifications]
    end
    
    WS_Start --> RS_Retry
    WS_Resume --> RS_Timeout
    WS_Cancel --> RS_Circuit
    WS_Get --> RS_Metrics
    
    RS_Retry --> WE_Execute
    RS_Timeout --> WE_Validate
    RS_Circuit --> WE_Activity
    RS_Metrics --> WE_Next
    
    WE_Execute --> FlowMgr
    WE_Execute --> LifeMgr
    WE_Execute --> StateMgr
    WE_Execute --> PersistMgr
    
    WE_Activity --> EventPub
    EventPub --> Outbox
    EventPub --> SignalR
    
    style WS_Start fill:#bbdefb
    style WE_Execute fill:#c8e6c9
    style RS_Retry fill:#ffcdd2
```

---

## Workflow Execution Flow

### 3. Complete Workflow Execution Flow

```mermaid
sequenceDiagram
    participant Client
    participant WorkflowService
    participant ResilienceService
    participant WorkflowEngine
    participant FlowControlManager
    participant ActivityFactory
    participant Activity
    participant PersistenceService
    participant EventPublisher
    participant OutboxDispatcher
    participant Database
    
    Client->>WorkflowService: StartWorkflowAsync()
    WorkflowService->>ResilienceService: ExecuteDatabaseOperationAsync()
    ResilienceService->>WorkflowEngine: StartWorkflowAsync()
    
    WorkflowEngine->>PersistenceService: GetWorkflowSchemaAsync()
    PersistenceService->>Database: Query WorkflowDefinition
    Database-->>PersistenceService: WorkflowSchema
    PersistenceService-->>WorkflowEngine: WorkflowSchema
    
    WorkflowEngine->>WorkflowEngine: InitializeWorkflowAsync()
    WorkflowEngine->>FlowControlManager: GetStartActivity()
    FlowControlManager-->>WorkflowEngine: StartActivity
    
    WorkflowEngine->>WorkflowEngine: ExecuteWorkflowAsync()
    
    loop For Each Activity
        WorkflowEngine->>ActivityFactory: CreateActivity(activityType)
        ActivityFactory-->>WorkflowEngine: ActivityInstance
        
        WorkflowEngine->>Activity: ExecuteAsync(context)
        Activity-->>WorkflowEngine: ActivityResult
        
        alt Activity Completed
            WorkflowEngine->>PersistenceService: SaveWorkflowInstanceAsync()
            PersistenceService->>Database: Update WorkflowInstance
            
            WorkflowEngine->>FlowControlManager: DetermineNextActivityAsync()
            FlowControlManager-->>WorkflowEngine: NextActivityId
            
            alt Has Next Activity
                WorkflowEngine->>WorkflowEngine: Continue to Next Activity
            else Workflow Complete
                WorkflowEngine->>WorkflowEngine: CompleteWorkflowAsync()
            end
            
        else Activity Pending
            WorkflowEngine->>PersistenceService: CreateBookmarkAsync()
            PersistenceService->>Database: Insert WorkflowBookmark
            WorkflowEngine-->>WorkflowService: Pending Result
            
        else Activity Failed
            WorkflowEngine->>WorkflowEngine: TransitionToFailedAsync()
            WorkflowEngine-->>WorkflowService: Failed Result
        end
    end
    
    WorkflowService->>EventPublisher: PublishWorkflowStartedAsync()
    EventPublisher->>Database: Insert WorkflowOutbox
    
    OutboxDispatcher->>Database: Get Pending Events
    OutboxDispatcher->>OutboxDispatcher: PublishEventAsync()
    OutboxDispatcher->>Database: Mark Event Processed
    
    WorkflowService-->>Client: WorkflowInstance
```

### 4. Activity Completion Flow

```mermaid
flowchart TD
    Start([Activity Completion Request]) --> Auth{Authentication Valid?}
    
    Auth -->|No| AuthError[Return 401 Unauthorized]
    Auth -->|Yes| GetWorkflow[Get Workflow Instance]
    
    GetWorkflow --> WorkflowExists{Workflow Exists?}
    WorkflowExists -->|No| NotFoundError[Return 404 Not Found]
    WorkflowExists -->|Yes| ValidateActivity[Validate Activity State]
    
    ValidateActivity --> ActivityValid{Activity Valid?}
    ActivityValid -->|No| ValidationError[Return 422 Validation Error]
    ActivityValid -->|Yes| CheckAssignment{User Assigned to Activity?}
    
    CheckAssignment -->|No| CheckPermission{Has Admin Permission?}
    CheckPermission -->|No| ForbiddenError[Return 403 Forbidden]
    CheckPermission -->|Yes| StartTransaction[Begin Database Transaction]
    CheckAssignment -->|Yes| StartTransaction
    
    StartTransaction --> CompleteActivity[Complete Activity Execution]
    CompleteActivity --> UpdateWorkflow[Update Workflow Instance]
    UpdateWorkflow --> CreateLog[Create Execution Log]
    CreateLog --> DetermineNext[Determine Next Activity]
    
    DetermineNext --> HasNext{Has Next Activity?}
    HasNext -->|Yes| StartNext[Start Next Activity]
    HasNext -->|No| CompleteWorkflow[Mark Workflow Complete]
    
    StartNext --> CommitTrans[Commit Transaction]
    CompleteWorkflow --> CommitTrans
    CommitTrans --> PublishEvents[Publish Events to Outbox]
    PublishEvents --> SendNotifications[Send Real-time Notifications]
    SendNotifications --> ReturnSuccess[Return 200 OK with Updated State]
    
    CompleteActivity --> ConcurrencyError{Concurrency Conflict?}
    ConcurrencyError -->|Yes| RollbackTrans[Rollback Transaction]
    RollbackTrans --> ReturnConflict[Return 409 Conflict]
    ConcurrencyError -->|No| UpdateWorkflow
    
    UpdateWorkflow --> TransactionError{Transaction Error?}
    TransactionError -->|Yes| RollbackTrans
    TransactionError -->|No| CreateLog
    
    style Start fill:#e3f2fd
    style ReturnSuccess fill:#e8f5e8
    style AuthError fill:#ffebee
    style NotFoundError fill:#ffebee
    style ValidationError fill:#fff3e0
    style ForbiddenError fill:#ffebee
    style ReturnConflict fill:#fff3e0
```

### 5. Background Services Processing Flow

```mermaid
graph TD
    subgraph "OutboxDispatcherService"
        ODS_Start[Service Started]
        ODS_GetEvents[Get Pending Events]
        ODS_ProcessBatch[Process Event Batch]
        ODS_Retry[Apply Retry Logic]
        ODS_DeadLetter[Move to Dead Letter]
        ODS_Wait[Wait Processing Interval]
    end
    
    subgraph "WorkflowTimerService"
        WTS_Start[Service Started]
        WTS_CheckTimers[Check Due Timers]
        WTS_ProcessTimer[Process Timer Bookmark]
        WTS_ResumeWorkflow[Resume Workflow]
        WTS_Timeout[Handle Workflow Timeouts]
        WTS_Wait[Wait Processing Interval]
    end
    
    subgraph "WorkflowCleanupService"
        WCS_Start[Service Started]
        WCS_CleanCompleted[Clean Completed Workflows]
        WCS_ArchiveLogs[Archive Old Logs]
        WCS_CleanOutbox[Clean Processed Events]
        WCS_Wait[Wait 24 Hours]
    end
    
    subgraph "Database"
        DB_Outbox[(WorkflowOutbox)]
        DB_Bookmarks[(WorkflowBookmark)]
        DB_Instances[(WorkflowInstance)]
        DB_Logs[(WorkflowExecutionLog)]
    end
    
    subgraph "External Services"
        NotificationSvc[Notification Service]
        SignalRSvc[SignalR Hub]
    end
    
    ODS_Start --> ODS_GetEvents
    ODS_GetEvents --> DB_Outbox
    DB_Outbox --> ODS_ProcessBatch
    ODS_ProcessBatch --> NotificationSvc
    ODS_ProcessBatch --> SignalRSvc
    ODS_ProcessBatch --> ODS_Retry
    ODS_Retry --> ODS_DeadLetter
    ODS_DeadLetter --> ODS_Wait
    ODS_Wait --> ODS_GetEvents
    
    WTS_Start --> WTS_CheckTimers
    WTS_CheckTimers --> DB_Bookmarks
    DB_Bookmarks --> WTS_ProcessTimer
    WTS_ProcessTimer --> WTS_ResumeWorkflow
    WTS_ResumeWorkflow --> WTS_Timeout
    WTS_Timeout --> DB_Instances
    WTS_Timeout --> WTS_Wait
    WTS_Wait --> WTS_CheckTimers
    
    WCS_Start --> WCS_CleanCompleted
    WCS_CleanCompleted --> DB_Instances
    WCS_CleanCompleted --> WCS_ArchiveLogs
    WCS_ArchiveLogs --> DB_Logs
    WCS_ArchiveLogs --> WCS_CleanOutbox
    WCS_CleanOutbox --> DB_Outbox
    WCS_CleanOutbox --> WCS_Wait
    WCS_Wait --> WCS_CleanCompleted
    
    style ODS_Start fill:#e1f5fe
    style WTS_Start fill:#e8f5e8
    style WCS_Start fill:#fff3e0
```

---

## Database Schema Diagrams

### 6. Entity Relationship Diagram

```mermaid
erDiagram
    WorkflowDefinition ||--o{ WorkflowInstance : "creates"
    WorkflowInstance ||--o{ WorkflowActivityExecution : "has"
    WorkflowInstance ||--o{ WorkflowBookmark : "has"
    WorkflowInstance ||--o{ WorkflowExecutionLog : "tracks"
    WorkflowInstance ||--o{ WorkflowOutbox : "publishes"
    WorkflowInstance ||--o{ WorkflowExternalCall : "makes"
    
    WorkflowDefinition {
        Guid Id PK
        string Name
        string Description
        int Version
        bool IsActive
        string Category
        string SchemaJson
        DateTime CreatedOn
        string CreatedBy
        byte[] ConcurrencyToken
    }
    
    WorkflowInstance {
        Guid Id PK
        Guid WorkflowDefinitionId FK
        string Name
        WorkflowStatus Status
        DateTime StartedOn
        DateTime CompletedOn
        string StartedBy
        string CompletedBy
        string CurrentActivityId
        string CurrentAssignee
        string VariablesJson
        string CorrelationId
        string ErrorMessage
        byte[] ConcurrencyToken
    }
    
    WorkflowActivityExecution {
        Guid Id PK
        Guid WorkflowInstanceId FK
        string ActivityId
        string ActivityName
        string ActivityType
        ActivityExecutionStatus Status
        DateTime StartedOn
        DateTime CompletedOn
        string AssignedTo
        string CompletedBy
        string InputDataJson
        string OutputDataJson
        string ErrorMessage
        byte[] ConcurrencyToken
    }
    
    WorkflowBookmark {
        Guid Id PK
        Guid WorkflowInstanceId FK
        string ActivityId
        BookmarkType Type
        string Key
        string Payload
        bool IsConsumed
        DateTime DueAt
        DateTime ConsumedAt
        DateTime CreatedAt
    }
    
    WorkflowExecutionLog {
        Guid Id PK
        Guid WorkflowInstanceId FK
        string ActivityId
        string Event
        DateTime At
        string DetailsJson
        string UserId
    }
    
    WorkflowOutbox {
        Guid Id PK
        DateTime OccurredAt
        string Type
        string Payload
        string HeadersJson
        int Attempts
        DateTime NextAttemptAt
        OutboxStatus Status
        DateTime ProcessedAt
        string ErrorMessage
        string CorrelationId
        Guid WorkflowInstanceId FK
        string ActivityId
        byte[] ConcurrencyToken
    }
    
    WorkflowExternalCall {
        Guid Id PK
        Guid WorkflowInstanceId FK
        string ActivityId
        string ServiceName
        string Operation
        string RequestPayload
        string ResponsePayload
        ExternalCallStatus Status
        DateTime InitiatedAt
        DateTime CompletedAt
        int AttemptCount
        string ErrorMessage
        byte[] ConcurrencyToken
    }
```

### 7. Database Indexes and Performance

```mermaid
graph TD
    subgraph "Performance Indexes"
        WI_Status[WorkflowInstance.Status]
        WI_Correlation[WorkflowInstance.CorrelationId]
        WI_StartedBy[WorkflowInstance.StartedBy]
        WI_CurrentAssignee[WorkflowInstance.CurrentAssignee]
        
        WAE_WorkflowId[WorkflowActivityExecution.WorkflowInstanceId]
        WAE_Status[WorkflowActivityExecution.Status]
        WAE_AssignedTo[WorkflowActivityExecution.AssignedTo]
        
        WB_DueAt[WorkflowBookmark.DueAt + IsConsumed]
        WB_Type[WorkflowBookmark.Type + IsConsumed]
        
        WO_Status[WorkflowOutbox.Status + NextAttemptAt]
        WO_Type[WorkflowOutbox.Type]
        
        WEL_WorkflowId[WorkflowExecutionLog.WorkflowInstanceId + At]
    end
    
    subgraph "Query Patterns"
        Q1[Find Active Workflows]
        Q2[Get User Tasks]
        Q3[Process Due Timers]
        Q4[Process Outbox Events]
        Q5[Audit Trail Query]
    end
    
    Q1 --> WI_Status
    Q2 --> WAE_AssignedTo
    Q2 --> WAE_Status
    Q3 --> WB_DueAt
    Q4 --> WO_Status
    Q5 --> WEL_WorkflowId
    
    style WI_Status fill:#e3f2fd
    style WAE_Status fill:#e8f5e8
    style WB_DueAt fill:#fff3e0
    style WO_Status fill:#fce4ec
```

---

## Background Services Architecture

### 8. Outbox Pattern Implementation

```mermaid
sequenceDiagram
    participant WF as Workflow Operation
    participant DB as Database Transaction
    participant Outbox as OutboxDispatcherService
    participant ExtSvc as External Service
    participant DLQ as Dead Letter Queue
    
    Note over WF,DB: Phase 1: Transactional Write
    WF->>DB: Begin Transaction
    WF->>DB: Update Workflow State
    WF->>DB: Insert Outbox Event
    WF->>DB: Commit Transaction
    WF-->>WF: Return Success
    
    Note over Outbox,ExtSvc: Phase 2: Async Processing
    loop Every 30 seconds
        Outbox->>DB: Get Pending Events (Batch=50)
        DB-->>Outbox: Pending Events List
        
        loop For Each Event
            Outbox->>Outbox: Mark as Processing
            Outbox->>DB: Update Event Status
            
            alt Successful Publish
                Outbox->>ExtSvc: Publish Event
                ExtSvc-->>Outbox: Success Response
                Outbox->>DB: Mark as Processed
                
            else Transient Failure
                Outbox->>ExtSvc: Publish Event
                ExtSvc-->>Outbox: Error Response
                Outbox->>Outbox: Calculate Retry Delay
                Outbox->>DB: Mark as Failed + Schedule Retry
                
            else Max Retries Exceeded
                Outbox->>DLQ: Move to Dead Letter
                Outbox->>DB: Mark as Dead Letter
            end
        end
        
        Outbox->>Outbox: Wait 30 seconds
    end
```

### 9. Timer Service Processing

```mermaid
flowchart TD
    TimerStart([Timer Service Starts]) --> CheckInterval{Every 60 seconds}
    
    CheckInterval --> GetDueTimers[Query Due Timer Bookmarks]
    GetDueTimers --> HasTimers{Any Due Timers?}
    
    HasTimers -->|No| WaitInterval[Wait 60 seconds]
    HasTimers -->|Yes| ProcessTimers[Process Each Timer]
    
    ProcessTimers --> ValidateTimer{Timer Still Valid?}
    ValidateTimer -->|No| SkipTimer[Skip Timer]
    ValidateTimer -->|Yes| ResumeWorkflow[Resume Workflow]
    
    ResumeWorkflow --> WorkflowResumed{Resume Successful?}
    WorkflowResumed -->|Yes| ConsumeBookmark[Mark Bookmark Consumed]
    WorkflowResumed -->|No| LogError[Log Resume Error]
    
    ConsumeBookmark --> CheckTimeout[Check Workflow Timeouts]
    LogError --> CheckTimeout
    SkipTimer --> CheckTimeout
    
    CheckTimeout --> HasTimeouts{Any Timed Out Workflows?}
    HasTimeouts -->|No| WaitInterval
    HasTimeouts -->|Yes| CancelTimedOut[Cancel Timed Out Workflows]
    
    CancelTimedOut --> NotifyTimeout[Notify Stakeholders]
    NotifyTimeout --> WaitInterval
    WaitInterval --> CheckInterval
    
    style TimerStart fill:#e3f2fd
    style ResumeWorkflow fill:#e8f5e8
    style CancelTimedOut fill:#ffebee
```

---

## API Integration Patterns

### 10. Authentication and Authorization Flow

```mermaid
sequenceDiagram
    participant Client
    participant APIGateway
    participant AuthService
    participant WorkflowAPI
    participant Database
    
    Client->>APIGateway: Request with JWT Token
    APIGateway->>AuthService: Validate Token
    
    alt Token Valid
        AuthService-->>APIGateway: User Claims
        APIGateway->>WorkflowAPI: Forward Request + User Context
        
        WorkflowAPI->>WorkflowAPI: Check User Permissions
        alt Has Required Permission
            WorkflowAPI->>Database: Execute Operation
            Database-->>WorkflowAPI: Result
            WorkflowAPI-->>APIGateway: Success Response
            APIGateway-->>Client: 200 OK
            
        else Insufficient Permission
            WorkflowAPI-->>APIGateway: 403 Forbidden
            APIGateway-->>Client: 403 Forbidden
        end
        
    else Token Invalid/Expired
        AuthService-->>APIGateway: Invalid Token
        APIGateway-->>Client: 401 Unauthorized
    end
```

### 11. Rate Limiting and Circuit Breaker

```mermaid
graph TD
    subgraph "Rate Limiting Layer"
        RL_Check{Within Rate Limit?}
        RL_Allow[Allow Request]
        RL_Deny[Return 429 Too Many Requests]
    end
    
    subgraph "Circuit Breaker Layer"
        CB_State{Circuit State}
        CB_Closed[Closed - Allow Request]
        CB_Open[Open - Fail Fast]
        CB_HalfOpen[Half-Open - Test Request]
    end
    
    subgraph "Resilience Service"
        RS_Retry[Retry Logic]
        RS_Timeout[Timeout Handling]
        RS_Success[Record Success]
        RS_Failure[Record Failure]
    end
    
    subgraph "Workflow Service"
        WS_Execute[Execute Workflow Operation]
        WS_Response[Return Response]
    end
    
    Request[Incoming Request] --> RL_Check
    RL_Check -->|Yes| CB_State
    RL_Check -->|No| RL_Deny
    
    CB_State -->|Closed| CB_Closed
    CB_State -->|Open| CB_Open
    CB_State -->|Half-Open| CB_HalfOpen
    
    CB_Closed --> RS_Retry
    CB_HalfOpen --> RS_Retry
    CB_Open --> Return503[Return 503 Service Unavailable]
    
    RS_Retry --> RS_Timeout
    RS_Timeout --> WS_Execute
    
    WS_Execute -->|Success| RS_Success
    WS_Execute -->|Failure| RS_Failure
    
    RS_Success --> CB_CloseCircuit[Close Circuit if Half-Open]
    RS_Failure --> CB_OpenCircuit[Open Circuit if Threshold Exceeded]
    
    CB_CloseCircuit --> WS_Response
    CB_OpenCircuit --> WS_Response
    
    style RL_Allow fill:#e8f5e8
    style CB_Closed fill:#e8f5e8
    style CB_Open fill:#ffebee
    style CB_HalfOpen fill:#fff3e0
```

---

## Error Handling Flows

### 12. Fault Handling and Recovery

```mermaid
flowchart TD
    Error[Exception Occurred] --> ClassifyError{Error Classification}
    
    ClassifyError -->|Transient| TransientError[Transient Error]
    ClassifyError -->|Business Logic| BusinessError[Business Logic Error]
    ClassifyError -->|System| SystemError[System Error]
    ClassifyError -->|External| ExternalError[External Service Error]
    
    TransientError --> RetryLogic{Retry Attempts < Max?}
    RetryLogic -->|Yes| ApplyBackoff[Apply Exponential Backoff]
    RetryLogic -->|No| PermanentFailure[Mark as Permanent Failure]
    
    ApplyBackoff --> RetryOperation[Retry Operation]
    RetryOperation --> Success{Operation Successful?}
    Success -->|Yes| RecordSuccess[Record Success Metrics]
    Success -->|No| RetryLogic
    
    BusinessError --> ValidateInput[Validate Business Rules]
    ValidateInput --> LogBusinessError[Log Business Error]
    LogBusinessError --> ReturnValidationError[Return 422 Validation Error]
    
    SystemError --> LogSystemError[Log System Error]
    LogSystemError --> NotifyAdmins[Notify System Administrators]
    NotifyAdmins --> ReturnSystemError[Return 500 Internal Server Error]
    
    ExternalError --> CheckCircuitBreaker{Circuit Breaker Open?}
    CheckCircuitBreaker -->|Yes| FailFast[Fail Fast - Return Error]
    CheckCircuitBreaker -->|No| RetryExternal[Retry External Call]
    
    RetryExternal --> ExternalSuccess{External Call Successful?}
    ExternalSuccess -->|Yes| CloseCircuit[Close Circuit Breaker]
    ExternalSuccess -->|No| OpenCircuit[Open Circuit Breaker]
    
    PermanentFailure --> CreateCompensation[Create Compensation Plan]
    CreateCompensation --> NotifyStakeholders[Notify Stakeholders]
    NotifyStakeholders --> ManualIntervention[Require Manual Intervention]
    
    RecordSuccess --> CompleteOperation[Complete Operation]
    ReturnValidationError --> CompleteOperation
    ReturnSystemError --> CompleteOperation
    FailFast --> CompleteOperation
    CloseCircuit --> CompleteOperation
    OpenCircuit --> CompleteOperation
    ManualIntervention --> CompleteOperation
    
    style Success fill:#e8f5e8
    style BusinessError fill:#fff3e0
    style SystemError fill:#ffebee
    style ExternalError fill:#e3f2fd
    style PermanentFailure fill:#ffebee
```

### 13. Concurrency Conflict Resolution

```mermaid
sequenceDiagram
    participant User1 as User 1
    participant User2 as User 2
    participant API1 as API Instance 1
    participant API2 as API Instance 2
    participant Database
    
    Note over User1,Database: Concurrent Modification Scenario
    
    User1->>API1: Complete Activity Request
    User2->>API2: Complete Same Activity Request
    
    API1->>Database: Begin Transaction
    API2->>Database: Begin Transaction
    
    API1->>Database: Read WorkflowInstance (Version=5)
    API2->>Database: Read WorkflowInstance (Version=5)
    
    API1->>API1: Process Activity Completion
    API2->>API2: Process Activity Completion
    
    API1->>Database: Update WorkflowInstance (Expected Version=5, New Version=6)
    Database-->>API1: Success - Row Updated
    
    API2->>Database: Update WorkflowInstance (Expected Version=5, New Version=6)
    Database-->>API2: Concurrency Conflict - Expected Version=5, Actual=6
    
    API1->>Database: Commit Transaction
    API2->>API2: Handle Concurrency Conflict
    API2->>Database: Rollback Transaction
    
    API1-->>User1: 200 OK - Activity Completed
    
    Note over API2,User2: Conflict Resolution
    API2->>API2: Wait with Exponential Backoff
    API2->>Database: Retry - Read Fresh WorkflowInstance (Version=6)
    API2->>API2: Validate Current State
    
    alt State Still Valid for Completion
        API2->>Database: Begin New Transaction
        API2->>Database: Update WorkflowInstance (Expected Version=6, New Version=7)
        Database-->>API2: Success
        API2->>Database: Commit Transaction
        API2-->>User2: 200 OK - Activity Completed
        
    else State No Longer Valid
        API2-->>User2: 409 Conflict - Workflow State Changed
    end
```

---

## Configuration Hierarchy

### 14. Configuration Sources and Precedence

```mermaid
graph TD
    subgraph "Configuration Hierarchy (Highest to Lowest Priority)"
        EnvVars[Environment Variables]
        UserSecrets[User Secrets - Development]
        EnvSpecific[appsettings.{Environment}.json]
        BaseSettings[appsettings.json]
        DefaultValues[Code Default Values]
    end
    
    subgraph "Configuration Sections"
        WorkflowResilience[WorkflowResilience]
        ConnectionStrings[ConnectionStrings]
        Logging[Logging]
        MockSupervisor[MockSupervisor]
    end
    
    subgraph "Validation Layer"
        ConfigValidation[Configuration Validation]
        StartupValidation[Startup Validation]
        RuntimeValidation[Runtime Health Checks]
    end
    
    subgraph "Application Startup"
        ConfigBuilder[ConfigurationBuilder]
        DependencyInjection[Service Registration]
        OptionsPattern[Options Pattern Binding]
        ApplicationStart[Application Start]
    end
    
    EnvVars --> ConfigBuilder
    UserSecrets --> ConfigBuilder
    EnvSpecific --> ConfigBuilder
    BaseSettings --> ConfigBuilder
    DefaultValues --> ConfigBuilder
    
    ConfigBuilder --> WorkflowResilience
    ConfigBuilder --> ConnectionStrings
    ConfigBuilder --> Logging
    ConfigBuilder --> MockSupervisor
    
    WorkflowResilience --> ConfigValidation
    ConnectionStrings --> ConfigValidation
    ConfigValidation --> StartupValidation
    StartupValidation --> RuntimeValidation
    
    ConfigBuilder --> DependencyInjection
    DependencyInjection --> OptionsPattern
    OptionsPattern --> ApplicationStart
    
    style EnvVars fill:#ffcdd2
    style BaseSettings fill:#e8f5e8
    style ConfigValidation fill:#fff3e0
```

### 15. Environment-Specific Configuration

```mermaid
graph LR
    subgraph "Development Environment"
        Dev_DB[LocalDB/SQL Express]
        Dev_Logging[Debug Level Logging]
        Dev_Resilience[Reduced Timeouts]
        Dev_Mock[Mock External Services]
    end
    
    subgraph "Staging Environment"
        Staging_DB[Staging SQL Server]
        Staging_Logging[Info Level Logging]
        Staging_Resilience[Standard Timeouts]
        Staging_External[Real External Services]
    end
    
    subgraph "Production Environment"
        Prod_DB[Production SQL Cluster]
        Prod_Logging[Warning Level Logging]
        Prod_Resilience[Extended Timeouts]
        Prod_Monitoring[Full Monitoring Stack]
        Prod_Security[Enhanced Security]
    end
    
    subgraph "Common Configuration"
        Base_Workflow[Workflow Schema]
        Base_Activities[Activity Definitions]
        Base_Validation[Validation Rules]
    end
    
    Base_Workflow --> Dev_DB
    Base_Workflow --> Staging_DB
    Base_Workflow --> Prod_DB
    
    Base_Activities --> Dev_Mock
    Base_Activities --> Staging_External
    Base_Activities --> Prod_Monitoring
    
    Base_Validation --> Dev_Logging
    Base_Validation --> Staging_Logging
    Base_Validation --> Prod_Logging
    
    style Dev_DB fill:#e3f2fd
    style Staging_DB fill:#fff3e0
    style Prod_DB fill:#e8f5e8
```

---

These visual diagrams provide comprehensive architectural and flow visualizations for the Workflow Module. They can be rendered using Mermaid-compatible tools and help teams understand the system's structure, data flow, and operational patterns at a glance.
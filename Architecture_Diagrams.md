# Architecture Diagrams for Collateral Appraisal System API

## 1. High-Level System Architecture

```mermaid
graph TB
    subgraph "Client Applications"
        Web[Web Application]
        Mobile[Mobile App]
        API_Client[API Client]
    end
    
    subgraph "API Gateway / Load Balancer"
        Gateway[API Gateway<br/>HTTPS:7111]
    end
    
    subgraph "Collateral Appraisal System API"
        subgraph "Bootstrapper/Api"
            Main[Main API<br/>ASP.NET Core<br/>Carter Framework]
        end
        
        subgraph "Modules"
            Request[Request Module<br/>Core Business Logic]
            Auth[Auth Module<br/>OAuth2/OpenID]
            Notification[Notification Module<br/>Event Processing]
            Collateral[Collateral Module<br/>Asset Management]
        end
        
        subgraph "Shared"
            DDD[DDD Infrastructure]
            CQRS[CQRS Contracts]
            Events[Event Abstractions]
        end
    end
    
    subgraph "Infrastructure Services"
        Database[(SQL Server 2022<br/>:1433)]
        Cache[(Redis 7<br/>:6379)]
        MessageBroker[RabbitMQ<br/>:5672/:15672]
        Logging[Seq Logging<br/>:5341]
    end
    
    Web --> Gateway
    Mobile --> Gateway
    API_Client --> Gateway
    Gateway --> Main
    
    Main --> Request
    Main --> Auth
    Main --> Notification
    Main --> Collateral
    
    Request --> DDD
    Auth --> DDD
    Notification --> CQRS
    Collateral --> Events
    
    Request --> Database
    Auth --> Database
    Request --> Cache
    Notification --> MessageBroker
    Request --> MessageBroker
    
    Main --> Logging
    Request --> Logging
    Auth --> Logging
    Notification --> Logging
```

## 2. Module Boundaries and Dependencies

```mermaid
graph LR
    subgraph "Request Module"
        RequestAPI[Request API<br/>Carter Endpoints]
        RequestHandlers[CQRS Handlers<br/>Commands/Queries]
        RequestDomain[Domain Layer<br/>Aggregates/VOs]
        RequestRepo[Repository<br/>+ Cache Decorator]
        RequestDB[(Request Database<br/>EF Core Context)]
    end
    
    subgraph "Auth Module"  
        AuthAPI[Auth API<br/>OAuth2 Endpoints]
        AuthHandlers[Auth Handlers<br/>Login/Logout]
        AuthDomain[Auth Domain<br/>Users/Tokens]
        AuthRepo[Auth Repository]
        AuthDB[(OpenIddict Database<br/>EF Core Context)]
    end
    
    subgraph "Notification Module"
        NotificationHandlers[Event Handlers<br/>Integration Events]
        NotificationServices[Notification Services<br/>Email/SMS/Push]
    end
    
    subgraph "Shared Infrastructure"
        SharedDDD[DDD Base Classes<br/>Aggregate/Entity/VO]
        SharedCQRS[CQRS Contracts<br/>ICommand/IQuery]
        SharedEvents[Event Infrastructure<br/>Domain/Integration]
    end
    
    subgraph "External Infrastructure"
        Redis[(Redis Cache)]
        RabbitMQ[RabbitMQ<br/>Message Broker]
        Seq[Seq Logging]
    end
    
    RequestAPI --> RequestHandlers
    RequestHandlers --> RequestDomain
    RequestHandlers --> RequestRepo
    RequestRepo --> RequestDB
    RequestRepo --> Redis
    
    AuthAPI --> AuthHandlers
    AuthHandlers --> AuthDomain
    AuthHandlers --> AuthRepo
    AuthRepo --> AuthDB
    
    RequestHandlers --> SharedEvents
    NotificationHandlers --> SharedEvents
    
    RequestDomain --> SharedDDD
    AuthDomain --> SharedDDD
    RequestHandlers --> SharedCQRS
    AuthHandlers --> SharedCQRS
    
    SharedEvents --> RabbitMQ
    RequestHandlers --> Seq
    AuthHandlers --> Seq
    NotificationHandlers --> Seq
```

## 3. CQRS Data Flow

```mermaid
sequenceDiagram
    participant Client
    participant API as Carter API
    participant MediatR
    participant Validator as FluentValidator
    participant Handler as Command Handler
    participant Domain as Domain Model
    participant Repo as Repository
    participant Cache as Redis Cache
    participant DB as SQL Server
    participant Events as Event Dispatcher
    participant RabbitMQ
    
    Client->>API: POST /requests
    API->>MediatR: Send CreateRequestCommand
    MediatR->>Validator: Validate Command
    alt Validation Fails
        Validator-->>Client: 400 Bad Request
    else Validation Passes
        Validator->>Handler: Execute Command
        Handler->>Domain: Create Request Aggregate
        Domain->>Domain: Apply Business Rules
        Domain->>Handler: Return Domain Model
        Handler->>Repo: Save Request
        Repo->>DB: Persist to Database
        Repo->>Cache: Update Cache
        Repo->>Events: Dispatch Domain Events
        Events->>RabbitMQ: Publish Integration Events
        Handler-->>Client: 201 Created
    end
    
    Note over RabbitMQ: Integration Events trigger<br/>cross-module processing
```

## 4. Event-Driven Architecture Flow

```mermaid
graph TB
    subgraph "Request Module"
        RequestAggregate[Request Aggregate<br/>Business Logic]
        RequestEvents[Domain Events<br/>RequestCreated<br/>RequestUpdated]
        RequestRepo[Request Repository<br/>Event Dispatch]
    end
    
    subgraph "Event Infrastructure"
        DomainEventDispatcher[Domain Event<br/>Dispatcher]
        IntegrationEventPublisher[Integration Event<br/>Publisher]
        RabbitMQ[RabbitMQ<br/>Message Broker]
    end
    
    subgraph "Notification Module"
        IntegrationEventHandler[Integration Event<br/>Handlers]
        NotificationService[Notification<br/>Services]
        EmailService[Email Service]
        SMSService[SMS Service]
    end
    
    subgraph "External Systems"
        EmailProvider[Email Provider<br/>SendGrid/SMTP]
        SMSProvider[SMS Provider<br/>Twilio/etc]
        AuditSystem[Audit System<br/>Compliance]
    end
    
    RequestAggregate --> RequestEvents
    RequestEvents --> RequestRepo
    RequestRepo --> DomainEventDispatcher
    DomainEventDispatcher --> IntegrationEventPublisher
    IntegrationEventPublisher --> RabbitMQ
    
    RabbitMQ --> IntegrationEventHandler
    IntegrationEventHandler --> NotificationService
    NotificationService --> EmailService
    NotificationService --> SMSService
    
    EmailService --> EmailProvider
    SMSService --> SMSProvider
    IntegrationEventHandler --> AuditSystem
```

## 5. Domain-Driven Design Structure

```mermaid
classDiagram
    class Request {
        <<Aggregate Root>>
        +RequestId: RequestId
        +RequestDetail: RequestDetail
        +Status: RequestStatus
        +CreatedAt: DateTime
        +UpdatedAt: DateTime
        +CreateRequest()
        +UpdateRequest()
        +ChangeStatus()
        +AddDomainEvent()
    }
    
    class RequestDetail {
        <<Value Object>>
        +LoanDetail: LoanDetail
        +Address: Address
        +Contact: Contact
        +Fee: Fee
        +Requestor: Requestor
    }
    
    class LoanDetail {
        <<Value Object>>
        +LoanAmount: decimal
        +SellingPrice: decimal
        +LoanType: string
    }
    
    class Address {
        <<Value Object>>
        +StreetAddress: string
        +City: string
        +State: string
        +ZipCode: string
        +Validate()
    }
    
    class Contact {
        <<Value Object>>
        +Email: string
        +Phone: string
        +Name: string
    }
    
    class RequestCreatedEvent {
        <<Domain Event>>
        +RequestId: RequestId
        +RequestDetail: RequestDetail
        +OccurredAt: DateTime
    }
    
    class IRequestRepository {
        <<Repository>>
        +GetByIdAsync()
        +SaveAsync()
        +DeleteAsync()
        +GetAllAsync()
    }
    
    class RequestRepository {
        +GetByIdAsync()
        +SaveAsync()
        +DeleteAsync()
        +GetAllAsync()
    }
    
    class CachedRequestRepository {
        <<Decorator>>
        +GetByIdAsync()
        +SaveAsync()
        +DeleteAsync()
        +GetAllAsync()
    }
    
    Request *-- RequestDetail
    RequestDetail *-- LoanDetail
    RequestDetail *-- Address
    RequestDetail *-- Contact
    Request ..> RequestCreatedEvent
    RequestRepository ..|> IRequestRepository
    CachedRequestRepository ..|> IRequestRepository
    CachedRequestRepository --> RequestRepository
```

## 6. Authentication and Authorization Flow

```mermaid
sequenceDiagram
    participant Client
    participant AuthAPI as Auth API
    participant OpenIddict
    participant AuthDB as Auth Database
    participant RequestAPI as Request API
    participant JWT as JWT Middleware
    
    Client->>AuthAPI: POST /auth/login
    AuthAPI->>AuthDB: Validate Credentials
    AuthDB-->>AuthAPI: User Valid
    AuthAPI->>OpenIddict: Generate Tokens
    OpenIddict-->>AuthAPI: Access/Refresh Tokens
    AuthAPI-->>Client: 200 OK + Tokens
    
    Note over Client: Store tokens securely
    
    Client->>RequestAPI: GET /requests<br/>Authorization: Bearer {token}
    RequestAPI->>JWT: Validate Token
    JWT->>OpenIddict: Verify Token Signature
    OpenIddict-->>JWT: Token Valid
    JWT->>RequestAPI: User Authorized
    RequestAPI-->>Client: 200 OK + Request Data
```

## 7. Caching Strategy

```mermaid
graph LR
    subgraph "Application Layer"
        Controller[Carter Endpoints]
        Handler[CQRS Handlers]
    end
    
    subgraph "Repository Layer"
        IRepo[IRequestRepository<br/>Interface]
        CachedRepo[CachedRequestRepository<br/>Decorator]
        BaseRepo[RequestRepository<br/>Base Implementation]
    end
    
    subgraph "Data Layer"
        Redis[(Redis Cache<br/>Distributed)]
        Database[(SQL Server<br/>Persistent)]
    end
    
    Controller --> Handler
    Handler --> IRepo
    IRepo --> CachedRepo
    CachedRepo --> BaseRepo
    CachedRepo --> Redis
    BaseRepo --> Database
    
    Note1[Cache Hit:<br/>Return from Redis]
    Note2[Cache Miss:<br/>Query DB, Store in Redis]
    
    CachedRepo -.-> Note1
    CachedRepo -.-> Note2
```

## 8. Development and Deployment Architecture

```mermaid
graph TB
    subgraph "Development Environment"
        Dev[Developer Machine<br/>Docker Desktop]
        DevServices[Docker Compose<br/>SQL Server + Redis + RabbitMQ + Seq]
        DevAPI[Local API<br/>dotnet run]
    end
    
    subgraph "Version Control"
        Git[Git Repository<br/>Feature Branches]
        CI[CI/CD Pipeline<br/>Build + Test + Deploy]
    end
    
    subgraph "Production Environment"
        LoadBalancer[Load Balancer<br/>HTTPS Termination]
        APIInstances[API Instances<br/>Docker Containers]
        ProdServices[Production Services<br/>Managed SQL + Redis + RabbitMQ]
        Monitoring[Monitoring<br/>Seq + APM + Metrics]
    end
    
    Dev --> DevServices
    Dev --> DevAPI
    Dev --> Git
    Git --> CI
    CI --> LoadBalancer
    LoadBalancer --> APIInstances
    APIInstances --> ProdServices
    APIInstances --> Monitoring
```
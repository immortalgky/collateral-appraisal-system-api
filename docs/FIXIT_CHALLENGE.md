# FixIt Booking System - DDD/CQRS Training Challenge

## 🎯 Challenge Overview

Welcome to the FixIt Booking System training challenge! This hands-on exercise will help you master Domain-Driven Design (DDD) and Command Query Responsibility Segregation (CQRS) patterns by building a real-world home repair and maintenance booking application.

### Business Context

**FixIt** is a home repair and maintenance service platform that connects homeowners with skilled technicians. Customers can:
- Browse available services (plumbing, electrical, HVAC, etc.)
- Book appointments with qualified technicians
- Track service progress
- Manage their booking history

Technicians can:
- Manage their availability
- Accept/decline bookings
- Complete services and collect payments
- Track their service history

### Learning Objectives

By completing this challenge, you will:
- ✅ Design and implement rich domain models with proper aggregate boundaries
- ✅ Apply CQRS pattern to separate read and write operations
- ✅ Use domain events for cross-aggregate communication
- ✅ Implement business rules and invariants within aggregates
- ✅ Create comprehensive tests for domain logic
- ✅ Follow the modular monolith architecture pattern
- ✅ Apply specification pattern for complex business rules

### Challenge Level

**Advanced** - This challenge includes:
- Multiple complex aggregates with rich behavior
- Domain events and integration events
- Complex business rules and validation
- Scheduling logic with conflict detection
- Comprehensive testing requirements

**NOT included** (to keep focus on DDD/CQRS):
- Sagas/Process Managers
- Event Sourcing
- Microservices communication patterns

---

## 📦 Domain Model

### Core Aggregates

#### 1. Booking (Aggregate Root)

The `Booking` aggregate is the heart of the system, managing the entire lifecycle of a service booking.

**Responsibilities:**
- Control booking lifecycle (Draft → Confirmed → InProgress → Completed/Cancelled)
- Enforce business rules for status transitions
- Manage booking details and service information
- Track technician assignment
- Coordinate scheduling

**Key Properties:**
- `BookingId`: Unique identifier
- `BookingDetail`: Value object containing booking information
- `Status`: BookingStatus value object (Draft, Confirmed, InProgress, Completed, Cancelled)
- `Customer`: Reference to customer (by ID)
- `Technician`: Reference to assigned technician (by ID)
- `ScheduledTimeSlot`: When the service is scheduled
- `ServiceInfo`: What service is being performed
- `CreatedOn`, `ConfirmedOn`, `CompletedOn`: Tracking timestamps

**Business Rules:**
- Cannot confirm booking without assigned technician
- Cannot assign technician if time slot conflicts with existing bookings
- Can only cancel bookings in Draft or Confirmed status
- Cannot modify confirmed bookings within 2 hours of scheduled time
- Completed bookings cannot be modified

**Domain Events:**
- `BookingCreatedEvent`
- `BookingConfirmedEvent`
- `TechnicianAssignedEvent`
- `BookingStartedEvent`
- `BookingCompletedEvent`
- `BookingCancelledEvent`
- `BookingRescheduledEvent`

**Example Structure:**
```csharp
public class Booking : Aggregate<long>
{
    public BookingDetail Detail { get; private set; }
    public BookingStatus Status { get; private set; }
    public long CustomerId { get; private set; }
    public long? TechnicianId { get; private set; }
    public TimeSlot ScheduledTimeSlot { get; private set; }
    public ServiceInfo Service { get; private set; }
    public Price EstimatedPrice { get; private set; }

    // Factory method
    public static Booking Create(
        long customerId,
        ServiceInfo service,
        TimeSlot requestedTimeSlot,
        Address serviceAddress,
        ContactInfo contactInfo)
    {
        // Validation and creation logic
        var booking = new Booking
        {
            // Initialize properties
            Status = BookingStatus.Draft
        };

        booking.AddDomainEvent(new BookingCreatedEvent(booking));
        return booking;
    }

    // Business methods
    public void AssignTechnician(long technicianId, TimeSlot availability)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != BookingStatus.Draft,
                "Can only assign technician to draft bookings")
            .AddErrorIf(TechnicianId.HasValue,
                "Booking already has assigned technician")
            .AddErrorIf(!availability.Contains(ScheduledTimeSlot),
                "Technician is not available for the scheduled time slot")
            .ThrowIfInvalid();

        TechnicianId = technicianId;
        AddDomainEvent(new TechnicianAssignedEvent(Id, technicianId));
    }

    public void Confirm()
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != BookingStatus.Draft,
                "Only draft bookings can be confirmed")
            .AddErrorIf(!TechnicianId.HasValue,
                "Cannot confirm booking without assigned technician")
            .AddErrorIf(ScheduledTimeSlot.StartsWithinHours(2),
                "Cannot confirm booking within 2 hours of scheduled time")
            .ThrowIfInvalid();

        Status = BookingStatus.Confirmed;
        AddDomainEvent(new BookingConfirmedEvent(Id, TechnicianId.Value, ScheduledTimeSlot));
    }

    public void StartService()
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != BookingStatus.Confirmed,
                "Only confirmed bookings can be started")
            .ThrowIfInvalid();

        Status = BookingStatus.InProgress;
        AddDomainEvent(new BookingStartedEvent(Id));
    }

    public void CompleteService(decimal actualPrice, string notes)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != BookingStatus.InProgress,
                "Only in-progress bookings can be completed")
            .ThrowIfInvalid();

        Status = BookingStatus.Completed;
        // Update final price and notes
        AddDomainEvent(new BookingCompletedEvent(Id, actualPrice));
    }

    public void Cancel(string reason)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status == BookingStatus.Completed,
                "Cannot cancel completed bookings")
            .AddErrorIf(Status == BookingStatus.Cancelled,
                "Booking is already cancelled")
            .ThrowIfInvalid();

        Status = BookingStatus.Cancelled;
        AddDomainEvent(new BookingCancelledEvent(Id, reason));
    }

    public void Reschedule(TimeSlot newTimeSlot)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != BookingStatus.Draft && Status != BookingStatus.Confirmed,
                "Can only reschedule draft or confirmed bookings")
            .AddErrorIf(newTimeSlot.StartTime < DateTime.UtcNow,
                "Cannot schedule booking in the past")
            .ThrowIfInvalid();

        var oldTimeSlot = ScheduledTimeSlot;
        ScheduledTimeSlot = newTimeSlot;
        AddDomainEvent(new BookingRescheduledEvent(Id, oldTimeSlot, newTimeSlot));
    }
}
```

#### 2. ServiceCatalog (Aggregate Root)

Manages the catalog of services offered by FixIt.

**Responsibilities:**
- Define available services with pricing and duration
- Organize services into categories
- Control service availability
- Define service requirements (skills, equipment)

**Key Properties:**
- `ServiceId`: Unique identifier
- `Name`: Service name
- `Description`: Detailed description
- `Category`: ServiceCategory value object
- `BasePrice`: Price value object
- `EstimatedDuration`: How long the service typically takes
- `RequiredSkills`: List of skills needed
- `IsActive`: Whether service is currently offered

**Business Rules:**
- Service name must be unique within category
- Price must be positive
- Duration must be at least 15 minutes
- Cannot archive service with active bookings
- Price changes only affect new bookings

**Domain Events:**
- `ServiceCreatedEvent`
- `ServiceUpdatedEvent`
- `ServicePriceChangedEvent`
- `ServiceArchivedEvent`

**Example Structure:**
```csharp
public class ServiceCatalog : Aggregate<long>
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public ServiceCategory Category { get; private set; }
    public Price BasePrice { get; private set; }
    public Duration EstimatedDuration { get; private set; }

    private readonly List<TechnicianSkill> _requiredSkills = [];
    public IReadOnlyList<TechnicianSkill> RequiredSkills => _requiredSkills.AsReadOnly();

    public bool IsActive { get; private set; }

    public static ServiceCatalog Create(
        string name,
        string description,
        ServiceCategory category,
        Price basePrice,
        Duration estimatedDuration,
        List<TechnicianSkill> requiredSkills)
    {
        // Validation and creation
        var service = new ServiceCatalog
        {
            // Initialize
            IsActive = true
        };

        service.AddDomainEvent(new ServiceCreatedEvent(service));
        return service;
    }

    public void UpdatePrice(Price newPrice, string reason)
    {
        RuleCheck.Valid()
            .AddErrorIf(!IsActive, "Cannot update price of archived service")
            .AddErrorIf(newPrice.Amount <= 0, "Price must be positive")
            .ThrowIfInvalid();

        var oldPrice = BasePrice;
        BasePrice = newPrice;
        AddDomainEvent(new ServicePriceChangedEvent(Id, oldPrice, newPrice, reason));
    }

    public void Archive()
    {
        RuleCheck.Valid()
            .AddErrorIf(!IsActive, "Service is already archived")
            .ThrowIfInvalid();

        IsActive = false;
        AddDomainEvent(new ServiceArchivedEvent(Id));
    }
}
```

#### 3. Customer (Aggregate Root)

Manages customer information and booking history.

**Responsibilities:**
- Store customer profile information
- Manage multiple service addresses
- Track customer preferences
- Maintain booking history reference

**Key Properties:**
- `CustomerId`: Unique identifier
- `PersonalInfo`: Name, email, phone (value object)
- `Addresses`: List of service addresses
- `PreferredContactMethod`: How to reach customer
- `IsActive`: Account status

**Business Rules:**
- Email must be unique and valid
- Must have at least one service address
- Cannot delete customer with active bookings
- Phone number must be valid format

**Domain Events:**
- `CustomerRegisteredEvent`
- `CustomerProfileUpdatedEvent`
- `CustomerAddressAddedEvent`
- `CustomerDeactivatedEvent`

**Example Structure:**
```csharp
public class Customer : Aggregate<long>
{
    public PersonalInfo PersonalInfo { get; private set; }

    private readonly List<Address> _serviceAddresses = [];
    public IReadOnlyList<Address> ServiceAddresses => _serviceAddresses.AsReadOnly();

    public ContactPreference PreferredContactMethod { get; private set; }
    public bool IsActive { get; private set; }

    public static Customer Register(
        PersonalInfo personalInfo,
        Address primaryAddress,
        ContactPreference preferredContact)
    {
        // Validation
        var customer = new Customer
        {
            PersonalInfo = personalInfo,
            PreferredContactMethod = preferredContact,
            IsActive = true
        };

        customer._serviceAddresses.Add(primaryAddress);
        customer.AddDomainEvent(new CustomerRegisteredEvent(customer));
        return customer;
    }

    public void AddServiceAddress(Address address)
    {
        RuleCheck.Valid()
            .AddErrorIf(!IsActive, "Cannot add address to inactive customer")
            .AddErrorIf(_serviceAddresses.Any(a => a.Equals(address)),
                "Address already exists")
            .ThrowIfInvalid();

        _serviceAddresses.Add(address);
        AddDomainEvent(new CustomerAddressAddedEvent(Id, address));
    }

    public void UpdateProfile(PersonalInfo newInfo)
    {
        PersonalInfo = newInfo;
        AddDomainEvent(new CustomerProfileUpdatedEvent(Id));
    }
}
```

#### 4. Technician (Aggregate Root)

Manages technician profiles, skills, and availability schedules.

**Responsibilities:**
- Store technician information and qualifications
- Manage availability schedule
- Track skills and certifications
- Control booking assignments

**Key Properties:**
- `TechnicianId`: Unique identifier
- `PersonalInfo`: Name, contact info
- `Skills`: List of skills and proficiency levels
- `Schedule`: Availability schedule
- `IsAvailable`: Current availability status
- `MaxDailyBookings`: Capacity limit

**Business Rules:**
- Cannot assign booking outside availability hours
- Cannot exceed max daily bookings
- Must have required skills for assigned service
- Cannot delete technician with active bookings
- Schedule changes must not conflict with confirmed bookings

**Domain Events:**
- `TechnicianRegisteredEvent`
- `TechnicianSkillAddedEvent`
- `TechnicianAvailabilityUpdatedEvent`
- `TechnicianBookingAssignedEvent`

**Example Structure:**
```csharp
public class Technician : Aggregate<long>
{
    public PersonalInfo PersonalInfo { get; private set; }

    private readonly List<TechnicianSkill> _skills = [];
    public IReadOnlyList<TechnicianSkill> Skills => _skills.AsReadOnly();

    private readonly List<AvailabilitySlot> _schedule = [];
    public IReadOnlyList<AvailabilitySlot> Schedule => _schedule.AsReadOnly();

    public bool IsAvailable { get; private set; }
    public int MaxDailyBookings { get; private set; }

    public static Technician Register(
        PersonalInfo personalInfo,
        List<TechnicianSkill> skills,
        int maxDailyBookings)
    {
        var technician = new Technician
        {
            PersonalInfo = personalInfo,
            MaxDailyBookings = maxDailyBookings,
            IsAvailable = true
        };

        technician._skills.AddRange(skills);
        technician.AddDomainEvent(new TechnicianRegisteredEvent(technician));
        return technician;
    }

    public void SetAvailability(DateTime date, TimeRange timeRange)
    {
        RuleCheck.Valid()
            .AddErrorIf(date < DateTime.Today,
                "Cannot set availability for past dates")
            .AddErrorIf(timeRange.Duration < TimeSpan.FromMinutes(30),
                "Availability slot must be at least 30 minutes")
            .ThrowIfInvalid();

        var slot = new AvailabilitySlot(date, timeRange);
        _schedule.Add(slot);
        AddDomainEvent(new TechnicianAvailabilityUpdatedEvent(Id, date, timeRange));
    }

    public bool IsAvailableForTimeSlot(TimeSlot requestedSlot)
    {
        return _schedule.Any(s => s.Contains(requestedSlot));
    }

    public bool HasRequiredSkills(List<TechnicianSkill> requiredSkills)
    {
        return requiredSkills.All(required =>
            _skills.Any(s => s.SkillType == required.SkillType &&
                            s.Level >= required.Level));
    }
}
```

---

### Value Objects

#### BookingDetail
Encapsulates all descriptive information about a booking.

```csharp
public record BookingDetail(
    string ServiceType,
    string Description,
    Address ServiceAddress,
    ContactInfo ContactInfo,
    string? SpecialInstructions
)
{
    public BookingDetail WithNewDescription(string description) =>
        this with { Description = description };
}
```

#### ServiceInfo
Information about the service being performed.

```csharp
public record ServiceInfo(
    long ServiceCatalogId,
    string ServiceName,
    ServiceCategory Category,
    Duration EstimatedDuration
);
```

#### Address
Standard address value object.

```csharp
public record Address(
    string Street,
    string City,
    string State,
    string PostalCode,
    string? Unit,
    string? Notes
)
{
    public string FullAddress =>
        $"{Street}{(Unit != null ? $" {Unit}" : "")}, {City}, {State} {PostalCode}";
}
```

#### ContactInfo
Contact information value object.

```csharp
public record ContactInfo(
    string Name,
    string Phone,
    string? AlternatePhone,
    string Email
);
```

#### TimeSlot
Represents a scheduled time period.

```csharp
public record TimeSlot
{
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }
    public TimeSpan Duration => EndTime - StartTime;

    private TimeSlot(DateTime startTime, DateTime endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
    }

    public static TimeSlot Create(DateTime startTime, TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(15))
            throw new ArgumentException("Time slot must be at least 15 minutes");

        return new TimeSlot(startTime, startTime.Add(duration));
    }

    public bool OverlapsWith(TimeSlot other)
    {
        return StartTime < other.EndTime && EndTime > other.StartTime;
    }

    public bool Contains(DateTime time)
    {
        return time >= StartTime && time < EndTime;
    }

    public bool StartsWithinHours(int hours)
    {
        return StartTime <= DateTime.UtcNow.AddHours(hours);
    }
}
```

#### Price
Money value object.

```csharp
public record Price
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Price(decimal amount, string currency = "USD")
    {
        Amount = amount;
        Currency = currency;
    }

    public static Price FromAmount(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Price cannot be negative");

        return new Price(amount, currency);
    }

    public Price Add(Price other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add prices in different currencies");

        return new Price(Amount + other.Amount, Currency);
    }
}
```

#### BookingStatus
Status value object with valid transitions.

```csharp
public class BookingStatus : ValueObject
{
    public string Code { get; }

    public static BookingStatus Draft => new(nameof(Draft).ToUpper());
    public static BookingStatus Confirmed => new(nameof(Confirmed).ToUpper());
    public static BookingStatus InProgress => new(nameof(InProgress).ToUpper());
    public static BookingStatus Completed => new(nameof(Completed).ToUpper());
    public static BookingStatus Cancelled => new(nameof(Cancelled).ToUpper());

    private BookingStatus(string code) => Code = code;

    public bool CanTransitionTo(BookingStatus newStatus)
    {
        return (Code, newStatus.Code) switch
        {
            ("DRAFT", "CONFIRMED") => true,
            ("DRAFT", "CANCELLED") => true,
            ("CONFIRMED", "INPROGRESS") => true,
            ("CONFIRMED", "CANCELLED") => true,
            ("INPROGRESS", "COMPLETED") => true,
            ("INPROGRESS", "CANCELLED") => true,
            _ => false
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Code;
    }
}
```

#### ServiceCategory
Service categorization.

```csharp
public class ServiceCategory : ValueObject
{
    public string Name { get; }

    public static ServiceCategory Plumbing => new(nameof(Plumbing));
    public static ServiceCategory Electrical => new(nameof(Electrical));
    public static ServiceCategory HVAC => new(nameof(HVAC));
    public static ServiceCategory Carpentry => new(nameof(Carpentry));
    public static ServiceCategory Painting => new(nameof(Painting));
    public static ServiceCategory Appliance => new(nameof(Appliance));
    public static ServiceCategory Landscaping => new(nameof(Landscaping));
    public static ServiceCategory Cleaning => new(nameof(Cleaning));

    private ServiceCategory(string name) => Name = name;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
    }
}
```

#### TechnicianSkill
Skill with proficiency level.

```csharp
public record TechnicianSkill(
    string SkillType,
    SkillLevel Level,
    DateTime? CertificationDate,
    DateTime? ExpiryDate
)
{
    public bool IsCertified => CertificationDate.HasValue &&
        (!ExpiryDate.HasValue || ExpiryDate.Value > DateTime.UtcNow);
}

public enum SkillLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
    Expert = 4
}
```

---

### Domain Events

Domain events capture important business occurrences within aggregates.

#### Booking Events

```csharp
public record BookingCreatedEvent(Booking Booking) : IDomainEvent;

public record BookingConfirmedEvent(
    long BookingId,
    long TechnicianId,
    TimeSlot ScheduledTime
) : IDomainEvent;

public record TechnicianAssignedEvent(
    long BookingId,
    long TechnicianId
) : IDomainEvent;

public record BookingStartedEvent(long BookingId) : IDomainEvent;

public record BookingCompletedEvent(
    long BookingId,
    decimal ActualPrice
) : IDomainEvent;

public record BookingCancelledEvent(
    long BookingId,
    string Reason
) : IDomainEvent;

public record BookingRescheduledEvent(
    long BookingId,
    TimeSlot OldTimeSlot,
    TimeSlot NewTimeSlot
) : IDomainEvent;
```

#### Service Catalog Events

```csharp
public record ServiceCreatedEvent(ServiceCatalog Service) : IDomainEvent;

public record ServicePriceChangedEvent(
    long ServiceId,
    Price OldPrice,
    Price NewPrice,
    string Reason
) : IDomainEvent;

public record ServiceArchivedEvent(long ServiceId) : IDomainEvent;
```

#### Customer Events

```csharp
public record CustomerRegisteredEvent(Customer Customer) : IDomainEvent;

public record CustomerProfileUpdatedEvent(long CustomerId) : IDomainEvent;

public record CustomerAddressAddedEvent(
    long CustomerId,
    Address Address
) : IDomainEvent;
```

#### Technician Events

```csharp
public record TechnicianRegisteredEvent(Technician Technician) : IDomainEvent;

public record TechnicianAvailabilityUpdatedEvent(
    long TechnicianId,
    DateTime Date,
    TimeRange TimeRange
) : IDomainEvent;

public record TechnicianSkillAddedEvent(
    long TechnicianId,
    TechnicianSkill Skill
) : IDomainEvent;
```

---

### Integration Events

Integration events enable communication between modules/aggregates through the message bus.

```csharp
// When booking is confirmed, notify notification module
public record BookingConfirmedIntegrationEvent : IntegrationEvent
{
    public long BookingId { get; set; }
    public long CustomerId { get; set; }
    public long TechnicianId { get; set; }
    public DateTime ScheduledStartTime { get; set; }
    public string ServiceName { get; set; }
    public string CustomerEmail { get; set; }
    public string TechnicianEmail { get; set; }
}

// When technician is assigned, update their schedule
public record TechnicianAssignedIntegrationEvent : IntegrationEvent
{
    public long BookingId { get; set; }
    public long TechnicianId { get; set; }
    public TimeSlot TimeSlot { get; set; }
}

// When service is completed, trigger payment
public record ServiceCompletedIntegrationEvent : IntegrationEvent
{
    public long BookingId { get; set; }
    public long CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; }
}
```

---

## 🎯 Core Features Implementation

### Feature 1: Core Booking Flow

Create and manage the complete booking lifecycle.

#### Commands

**CreateBookingCommand**
```csharp
public record CreateBookingCommand(
    long CustomerId,
    long ServiceCatalogId,
    DateTime RequestedStartTime,
    TimeSpan EstimatedDuration,
    AddressDto ServiceAddress,
    ContactInfoDto ContactInfo,
    string? SpecialInstructions
) : ICommand<CreateBookingResult>;

public record CreateBookingResult(long BookingId);
```

**CreateBookingCommandHandler**
```csharp
internal class CreateBookingCommandHandler(
    IBookingRepository bookingRepository,
    IServiceCatalogRepository serviceCatalogRepository)
    : ICommandHandler<CreateBookingCommand, CreateBookingResult>
{
    public async Task<CreateBookingResult> Handle(
        CreateBookingCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load service catalog to get service info
        var service = await serviceCatalogRepository.GetByIdAsync(
            command.ServiceCatalogId,
            cancellationToken);

        if (service == null)
            throw new NotFoundException("ServiceCatalog", command.ServiceCatalogId);

        if (!service.IsActive)
            throw new BusinessRuleException("Cannot book inactive service");

        // 2. Create booking aggregate
        var timeSlot = TimeSlot.Create(
            command.RequestedStartTime,
            command.EstimatedDuration);

        var booking = Booking.Create(
            command.CustomerId,
            ServiceInfo.FromCatalog(service),
            timeSlot,
            Address.From(command.ServiceAddress),
            ContactInfo.From(command.ContactInfo));

        // 3. Save
        await bookingRepository.AddAsync(booking, cancellationToken);
        await bookingRepository.SaveChangesAsync(cancellationToken);

        return new CreateBookingResult(booking.Id);
    }
}
```

**CreateBookingCommandValidator**
```csharp
public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("CustomerId is required");

        RuleFor(x => x.ServiceCatalogId)
            .GreaterThan(0)
            .WithMessage("ServiceCatalogId is required");

        RuleFor(x => x.RequestedStartTime)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Booking must be scheduled in the future");

        RuleFor(x => x.EstimatedDuration)
            .GreaterThanOrEqualTo(TimeSpan.FromMinutes(15))
            .WithMessage("Duration must be at least 15 minutes");

        RuleFor(x => x.ServiceAddress)
            .NotNull()
            .WithMessage("Service address is required");

        RuleFor(x => x.ContactInfo)
            .NotNull()
            .WithMessage("Contact information is required");
    }
}
```

**AssignTechnicianCommand**
```csharp
public record AssignTechnicianCommand(
    long BookingId,
    long TechnicianId
) : ICommand;

internal class AssignTechnicianCommandHandler(
    IBookingRepository bookingRepository,
    ITechnicianRepository technicianRepository)
    : ICommandHandler<AssignTechnicianCommand>
{
    public async Task<Unit> Handle(
        AssignTechnicianCommand command,
        CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(
            command.BookingId,
            cancellationToken);

        var technician = await technicianRepository.GetByIdAsync(
            command.TechnicianId,
            cancellationToken);

        // Check technician has required skills
        if (!technician.HasRequiredSkills(booking.Service.RequiredSkills))
            throw new BusinessRuleException("Technician lacks required skills");

        // Check technician is available
        if (!technician.IsAvailableForTimeSlot(booking.ScheduledTimeSlot))
            throw new BusinessRuleException("Technician is not available for this time slot");

        // Assign
        booking.AssignTechnician(command.TechnicianId, technician.GetAvailability());

        await bookingRepository.UpdateAsync(booking, cancellationToken);
        await bookingRepository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
```

**ConfirmBookingCommand**
```csharp
public record ConfirmBookingCommand(long BookingId) : ICommand;

internal class ConfirmBookingCommandHandler(IBookingRepository bookingRepository)
    : ICommandHandler<ConfirmBookingCommand>
{
    public async Task<Unit> Handle(
        ConfirmBookingCommand command,
        CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(
            command.BookingId,
            cancellationToken);

        booking.Confirm();

        await bookingRepository.UpdateAsync(booking, cancellationToken);
        await bookingRepository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
```

**CompleteBookingCommand**
```csharp
public record CompleteBookingCommand(
    long BookingId,
    decimal ActualPrice,
    string? CompletionNotes
) : ICommand;
```

**CancelBookingCommand**
```csharp
public record CancelBookingCommand(
    long BookingId,
    string CancellationReason
) : ICommand;
```

#### Queries

**GetBookingQuery**
```csharp
public record GetBookingQuery(long BookingId) : IQuery<GetBookingResult>;

public record GetBookingResult(BookingDto Booking);

public record BookingDto(
    long Id,
    long CustomerId,
    string CustomerName,
    long? TechnicianId,
    string? TechnicianName,
    string Status,
    ServiceInfoDto Service,
    TimeSlotDto ScheduledTime,
    AddressDto ServiceAddress,
    decimal EstimatedPrice,
    decimal? ActualPrice,
    DateTime CreatedOn,
    DateTime? ConfirmedOn,
    DateTime? CompletedOn
);

internal class GetBookingQueryHandler(IBookingReadRepository repository)
    : IQueryHandler<GetBookingQuery, GetBookingResult>
{
    public async Task<GetBookingResult> Handle(
        GetBookingQuery query,
        CancellationToken cancellationToken)
    {
        var booking = await repository.GetBookingWithDetailsAsync(
            query.BookingId,
            cancellationToken);

        if (booking == null)
            throw new NotFoundException("Booking", query.BookingId);

        var dto = booking.Adapt<BookingDto>();
        return new GetBookingResult(dto);
    }
}
```

**GetBookingsQuery (Paginated)**
```csharp
public record GetBookingsQuery(
    PaginationRequest Pagination,
    BookingFilterDto? Filters
) : IQuery<GetBookingsResult>;

public record BookingFilterDto(
    long? CustomerId,
    long? TechnicianId,
    string? Status,
    DateTime? FromDate,
    DateTime? ToDate
);

public record GetBookingsResult(PaginatedResult<BookingDto> Bookings);
```

**GetCustomerBookingHistoryQuery**
```csharp
public record GetCustomerBookingHistoryQuery(
    long CustomerId,
    PaginationRequest Pagination
) : IQuery<GetCustomerBookingHistoryResult>;
```

---

### Feature 2: Service Catalog Management

#### Commands

**CreateServiceCommand**
```csharp
public record CreateServiceCommand(
    string Name,
    string Description,
    string Category,
    decimal BasePrice,
    int EstimatedDurationMinutes,
    List<string> RequiredSkills
) : ICommand<CreateServiceResult>;

public record CreateServiceResult(long ServiceId);
```

**UpdateServiceCommand**
```csharp
public record UpdateServiceCommand(
    long ServiceId,
    string Name,
    string Description,
    int EstimatedDurationMinutes
) : ICommand;
```

**UpdateServicePriceCommand**
```csharp
public record UpdateServicePriceCommand(
    long ServiceId,
    decimal NewPrice,
    string Reason
) : ICommand;
```

**ArchiveServiceCommand**
```csharp
public record ArchiveServiceCommand(long ServiceId) : ICommand;
```

#### Queries

**GetServicesQuery**
```csharp
public record GetServicesQuery(
    PaginationRequest Pagination,
    ServiceFilterDto? Filters
) : IQuery<GetServicesResult>;

public record ServiceFilterDto(
    string? Category,
    bool? IsActive,
    decimal? MinPrice,
    decimal? MaxPrice
);

public record GetServicesResult(PaginatedResult<ServiceDto> Services);
```

**GetServicesByCategoryQuery**
```csharp
public record GetServicesByCategoryQuery(
    string Category,
    bool ActiveOnly = true
) : IQuery<GetServicesByCategoryResult>;
```

---

### Feature 3: Customer Management

#### Commands

**RegisterCustomerCommand**
```csharp
public record RegisterCustomerCommand(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    AddressDto PrimaryAddress,
    string PreferredContactMethod
) : ICommand<RegisterCustomerResult>;

public record RegisterCustomerResult(long CustomerId);
```

**UpdateCustomerProfileCommand**
```csharp
public record UpdateCustomerProfileCommand(
    long CustomerId,
    string FirstName,
    string LastName,
    string Phone,
    string PreferredContactMethod
) : ICommand;
```

**AddCustomerAddressCommand**
```csharp
public record AddCustomerAddressCommand(
    long CustomerId,
    AddressDto Address
) : ICommand;
```

#### Queries

**GetCustomerQuery**
```csharp
public record GetCustomerQuery(long CustomerId) : IQuery<GetCustomerResult>;

public record GetCustomerResult(CustomerDto Customer);

public record CustomerDto(
    long Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    List<AddressDto> ServiceAddresses,
    string PreferredContactMethod,
    bool IsActive,
    DateTime RegisteredOn
);
```

**GetCustomerBookingHistoryQuery**
```csharp
public record GetCustomerBookingHistoryQuery(
    long CustomerId,
    PaginationRequest Pagination
) : IQuery<GetCustomerBookingHistoryResult>;
```

---

### Feature 4: Scheduling & Availability

#### Commands

**SetTechnicianAvailabilityCommand**
```csharp
public record SetTechnicianAvailabilityCommand(
    long TechnicianId,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime
) : ICommand;
```

**BlockTimeSlotCommand**
```csharp
public record BlockTimeSlotCommand(
    long TechnicianId,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Reason
) : ICommand;
```

#### Queries

**GetTechnicianScheduleQuery**
```csharp
public record GetTechnicianScheduleQuery(
    long TechnicianId,
    DateTime FromDate,
    DateTime ToDate
) : IQuery<GetTechnicianScheduleResult>;

public record GetTechnicianScheduleResult(
    long TechnicianId,
    string TechnicianName,
    List<AvailabilitySlotDto> AvailableSlots,
    List<BookingSlotDto> BookedSlots
);

public record AvailabilitySlotDto(
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    bool IsBooked
);

public record BookingSlotDto(
    long BookingId,
    DateTime StartTime,
    DateTime EndTime,
    string ServiceName,
    string CustomerName,
    string Status
);
```

**FindAvailableTechniciansQuery**
```csharp
public record FindAvailableTechniciansQuery(
    long ServiceCatalogId,
    DateTime RequestedStartTime,
    TimeSpan Duration
) : IQuery<FindAvailableTechniciansResult>;

public record FindAvailableTechniciansResult(
    List<TechnicianAvailabilityDto> AvailableTechnicians
);

public record TechnicianAvailabilityDto(
    long TechnicianId,
    string Name,
    List<string> Skills,
    bool HasRequiredSkills,
    int BookingsToday,
    int MaxDailyBookings
);
```

---

## 🧩 Specifications Pattern

Use specifications for complex business rules and queries.

### BookingSpecifications

**BookingsByCustomerSpecification**
```csharp
public class BookingsByCustomerSpecification : Specification<Booking>
{
    private readonly long _customerId;

    public BookingsByCustomerSpecification(long customerId)
    {
        _customerId = customerId;
    }

    public override Expression<Func<Booking, bool>> ToExpression()
    {
        return booking => booking.CustomerId == _customerId;
    }
}
```

**BookingsByStatusSpecification**
```csharp
public class BookingsByStatusSpecification : Specification<Booking>
{
    private readonly BookingStatus _status;

    public BookingsByStatusSpecification(BookingStatus status)
    {
        _status = status;
    }

    public override Expression<Func<Booking, bool>> ToExpression()
    {
        return booking => booking.Status == _status;
    }
}
```

**BookingsInDateRangeSpecification**
```csharp
public class BookingsInDateRangeSpecification : Specification<Booking>
{
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;

    public BookingsInDateRangeSpecification(DateTime fromDate, DateTime toDate)
    {
        _fromDate = fromDate;
        _toDate = toDate;
    }

    public override Expression<Func<Booking, bool>> ToExpression()
    {
        return booking =>
            booking.ScheduledTimeSlot.StartTime >= _fromDate &&
            booking.ScheduledTimeSlot.StartTime < _toDate;
    }
}
```

**Usage Example:**
```csharp
// Combine specifications
public async Task<List<Booking>> GetCustomerActiveBookings(long customerId)
{
    var customerSpec = new BookingsByCustomerSpecification(customerId);
    var activeSpec = new BookingsByStatusSpecification(BookingStatus.Confirmed)
        | new BookingsByStatusSpecification(BookingStatus.InProgress);

    var combinedSpec = customerSpec & activeSpec;

    return await _repository.FindAsync(combinedSpec);
}
```

### TechnicianSpecifications

**TechniciansWithSkillSpecification**
```csharp
public class TechniciansWithSkillSpecification : Specification<Technician>
{
    private readonly string _skillType;
    private readonly SkillLevel _minimumLevel;

    public TechniciansWithSkillSpecification(
        string skillType,
        SkillLevel minimumLevel = SkillLevel.Beginner)
    {
        _skillType = skillType;
        _minimumLevel = minimumLevel;
    }

    public override Expression<Func<Technician, bool>> ToExpression()
    {
        return technician => technician.Skills.Any(s =>
            s.SkillType == _skillType &&
            s.Level >= _minimumLevel);
    }
}
```

**AvailableTechniciansSpecification**
```csharp
public class AvailableTechniciansSpecification : Specification<Technician>
{
    private readonly TimeSlot _requestedSlot;

    public AvailableTechniciansSpecification(TimeSlot requestedSlot)
    {
        _requestedSlot = requestedSlot;
    }

    public override Expression<Func<Technician, bool>> ToExpression()
    {
        return technician =>
            technician.IsAvailable &&
            technician.Schedule.Any(slot =>
                slot.Date == _requestedSlot.StartTime.Date &&
                slot.TimeRange.Start <= _requestedSlot.StartTime.TimeOfDay &&
                slot.TimeRange.End >= _requestedSlot.EndTime.TimeOfDay);
    }
}
```

---

## 🏗️ Repository Pattern

### IBookingRepository

```csharp
public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Booking?> GetBookingWithDetailsAsync(long id, CancellationToken cancellationToken = default);
    Task<List<Booking>> FindAsync(Specification<Booking> specification, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

### BookingRepository Implementation

```csharp
public class BookingRepository(FixItDbContext dbContext) : IBookingRepository
{
    public async Task<Booking?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Technician)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Booking?> GetBookingWithDetailsAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Technician)
            .Include(b => b.Service)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<List<Booking>> FindAsync(
        Specification<Booking> specification,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookings
            .Where(specification.ToExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Bookings.AddAsync(booking, cancellationToken);
    }

    public Task UpdateAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        dbContext.Bookings.Update(booking);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var booking = await GetByIdAsync(id, cancellationToken);
        if (booking != null)
        {
            dbContext.Bookings.Remove(booking);
        }
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

### Caching Decorator

```csharp
public class CachedBookingRepository : IBookingRepository
{
    private readonly IBookingRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedBookingRepository> _logger;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(15);

    public CachedBookingRepository(
        IBookingRepository repository,
        IDistributedCache cache,
        ILogger<CachedBookingRepository> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Booking?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"booking:{id}";
        var cachedJson = await _cache.GetStringAsync(cacheKey, cancellationToken);

        if (!string.IsNullOrEmpty(cachedJson))
        {
            _logger.LogDebug("Cache hit for booking {BookingId}", id);
            return JsonSerializer.Deserialize<Booking>(cachedJson);
        }

        var booking = await _repository.GetByIdAsync(id, cancellationToken);

        if (booking != null)
        {
            var serialized = JsonSerializer.Serialize(booking);
            await _cache.SetStringAsync(
                cacheKey,
                serialized,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheExpiry
                },
                cancellationToken);

            _logger.LogDebug("Cached booking {BookingId}", id);
        }

        return booking;
    }

    public async Task UpdateAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(booking, cancellationToken);

        // Invalidate cache
        var cacheKey = $"booking:{booking.Id}";
        await _cache.RemoveAsync(cacheKey, cancellationToken);
        _logger.LogDebug("Invalidated cache for booking {BookingId}", booking.Id);
    }

    // Other methods delegate to _repository
    public Task<Booking?> GetBookingWithDetailsAsync(long id, CancellationToken cancellationToken = default)
        => _repository.GetBookingWithDetailsAsync(id, cancellationToken);

    public Task<List<Booking>> FindAsync(Specification<Booking> specification, CancellationToken cancellationToken = default)
        => _repository.FindAsync(specification, cancellationToken);

    public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
        => _repository.AddAsync(booking, cancellationToken);

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _repository.SaveChangesAsync(cancellationToken);
}
```

---

## 🧪 Testing Requirements

### Unit Tests - Aggregate Behavior

**BookingAggregateTests.cs**
```csharp
public class BookingAggregateTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateBookingInDraftStatus()
    {
        // Arrange
        var customerId = 1L;
        var service = CreateTestServiceInfo();
        var timeSlot = TimeSlot.Create(DateTime.UtcNow.AddDays(1), TimeSpan.FromHours(2));
        var address = CreateTestAddress();
        var contact = CreateTestContactInfo();

        // Act
        var booking = Booking.Create(customerId, service, timeSlot, address, contact);

        // Assert
        booking.Should().NotBeNull();
        booking.Status.Should().Be(BookingStatus.Draft);
        booking.CustomerId.Should().Be(customerId);
        booking.ScheduledTimeSlot.Should().Be(timeSlot);
        booking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BookingCreatedEvent>();
    }

    [Fact]
    public void AssignTechnician_WhenBookingIsDraft_ShouldAssignSuccessfully()
    {
        // Arrange
        var booking = CreateTestBooking();
        var technicianId = 5L;
        var availability = CreateAvailabilityForTimeSlot(booking.ScheduledTimeSlot);

        // Act
        booking.AssignTechnician(technicianId, availability);

        // Assert
        booking.TechnicianId.Should().Be(technicianId);
        booking.DomainEvents.Should().Contain(e =>
            e is TechnicianAssignedEvent evt &&
            evt.TechnicianId == technicianId);
    }

    [Fact]
    public void AssignTechnician_WhenAlreadyAssigned_ShouldThrowException()
    {
        // Arrange
        var booking = CreateTestBooking();
        var availability = CreateAvailabilityForTimeSlot(booking.ScheduledTimeSlot);
        booking.AssignTechnician(5L, availability);

        // Act
        var act = () => booking.AssignTechnician(6L, availability);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*already has assigned technician*");
    }

    [Fact]
    public void Confirm_WithoutAssignedTechnician_ShouldThrowException()
    {
        // Arrange
        var booking = CreateTestBooking();

        // Act
        var act = () => booking.Confirm();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*without assigned technician*");
    }

    [Fact]
    public void Confirm_WithAssignedTechnician_ShouldChangeStatusToConfirmed()
    {
        // Arrange
        var booking = CreateTestBooking();
        var availability = CreateAvailabilityForTimeSlot(booking.ScheduledTimeSlot);
        booking.AssignTechnician(5L, availability);

        // Act
        booking.Confirm();

        // Assert
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.DomainEvents.Should().Contain(e => e is BookingConfirmedEvent);
    }

    [Fact]
    public void Cancel_WhenCompleted_ShouldThrowException()
    {
        // Arrange
        var booking = CreateCompletedBooking();

        // Act
        var act = () => booking.Cancel("Customer request");

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot cancel completed bookings*");
    }

    [Fact]
    public void Reschedule_WhenInProgress_ShouldThrowException()
    {
        // Arrange
        var booking = CreateInProgressBooking();
        var newTimeSlot = TimeSlot.Create(
            DateTime.UtcNow.AddDays(2),
            TimeSpan.FromHours(2));

        // Act
        var act = () => booking.Reschedule(newTimeSlot);

        // Assert
        act.Should().Throw<DomainException>();
    }

    // Helper methods
    private Booking CreateTestBooking()
    {
        return Booking.Create(
            customerId: 1L,
            service: CreateTestServiceInfo(),
            requestedTimeSlot: TimeSlot.Create(
                DateTime.UtcNow.AddDays(1),
                TimeSpan.FromHours(2)),
            serviceAddress: CreateTestAddress(),
            contactInfo: CreateTestContactInfo());
    }

    private ServiceInfo CreateTestServiceInfo()
    {
        return new ServiceInfo(
            ServiceCatalogId: 1L,
            ServiceName: "Plumbing Repair",
            Category: ServiceCategory.Plumbing,
            EstimatedDuration: TimeSpan.FromHours(2));
    }

    private Address CreateTestAddress()
    {
        return new Address(
            Street: "123 Main St",
            City: "Springfield",
            State: "IL",
            PostalCode: "62701",
            Unit: null,
            Notes: null);
    }

    private ContactInfo CreateTestContactInfo()
    {
        return new ContactInfo(
            Name: "John Doe",
            Phone: "555-1234",
            AlternatePhone: null,
            Email: "john@example.com");
    }
}
```

### Unit Tests - Value Objects

**ValueObjectTests.cs**
```csharp
public class TimeSlotTests
{
    [Fact]
    public void Create_WithValidDuration_ShouldCreateTimeSlot()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(1);
        var duration = TimeSpan.FromHours(2);

        // Act
        var timeSlot = TimeSlot.Create(startTime, duration);

        // Assert
        timeSlot.StartTime.Should().Be(startTime);
        timeSlot.Duration.Should().Be(duration);
        timeSlot.EndTime.Should().Be(startTime.Add(duration));
    }

    [Fact]
    public void Create_WithTooShortDuration_ShouldThrowException()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var duration = TimeSpan.FromMinutes(10);

        // Act
        var act = () => TimeSlot.Create(startTime, duration);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 15 minutes*");
    }

    [Fact]
    public void OverlapsWith_WhenSlotsOverlap_ShouldReturnTrue()
    {
        // Arrange
        var slot1 = TimeSlot.Create(
            DateTime.Today.AddHours(10),
            TimeSpan.FromHours(2)); // 10:00 - 12:00
        var slot2 = TimeSlot.Create(
            DateTime.Today.AddHours(11),
            TimeSpan.FromHours(2)); // 11:00 - 13:00

        // Act & Assert
        slot1.OverlapsWith(slot2).Should().BeTrue();
        slot2.OverlapsWith(slot1).Should().BeTrue();
    }

    [Fact]
    public void OverlapsWith_WhenSlotsDoNotOverlap_ShouldReturnFalse()
    {
        // Arrange
        var slot1 = TimeSlot.Create(
            DateTime.Today.AddHours(10),
            TimeSpan.FromHours(2)); // 10:00 - 12:00
        var slot2 = TimeSlot.Create(
            DateTime.Today.AddHours(14),
            TimeSpan.FromHours(2)); // 14:00 - 16:00

        // Act & Assert
        slot1.OverlapsWith(slot2).Should().BeFalse();
    }
}

public class PriceTests
{
    [Fact]
    public void FromAmount_WithNegativeAmount_ShouldThrowException()
    {
        // Act
        var act = () => Price.FromAmount(-10.00m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void Add_WithSameCurrency_ShouldReturnSum()
    {
        // Arrange
        var price1 = Price.FromAmount(50.00m, "USD");
        var price2 = Price.FromAmount(30.00m, "USD");

        // Act
        var result = price1.Add(price2);

        // Assert
        result.Amount.Should().Be(80.00m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_WithDifferentCurrency_ShouldThrowException()
    {
        // Arrange
        var price1 = Price.FromAmount(50.00m, "USD");
        var price2 = Price.FromAmount(30.00m, "EUR");

        // Act
        var act = () => price1.Add(price2);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }
}

public class BookingStatusTests
{
    [Fact]
    public void CanTransitionTo_FromDraftToConfirmed_ShouldReturnTrue()
    {
        // Arrange
        var status = BookingStatus.Draft;

        // Act & Assert
        status.CanTransitionTo(BookingStatus.Confirmed).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromCompletedToInProgress_ShouldReturnFalse()
    {
        // Arrange
        var status = BookingStatus.Completed;

        // Act & Assert
        status.CanTransitionTo(BookingStatus.InProgress).Should().BeFalse();
    }
}
```

### Integration Tests - Command Handlers

**CreateBookingIntegrationTests.cs**
```csharp
public class CreateBookingIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateBooking_WithValidData_ShouldPersistBooking()
    {
        // Arrange
        await SeedServiceCatalog();

        var command = new CreateBookingCommand(
            CustomerId: 1L,
            ServiceCatalogId: 1L,
            RequestedStartTime: DateTime.UtcNow.AddDays(1),
            EstimatedDuration: TimeSpan.FromHours(2),
            ServiceAddress: new AddressDto(
                "123 Main St",
                "Springfield",
                "IL",
                "62701",
                null,
                null),
            ContactInfo: new ContactInfoDto(
                "John Doe",
                "555-1234",
                null,
                "john@example.com"),
            SpecialInstructions: null);

        var handler = GetService<ICommandHandler<CreateBookingCommand, CreateBookingResult>>();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BookingId.Should().BeGreaterThan(0);

        // Verify persistence
        var repository = GetService<IBookingRepository>();
        var savedBooking = await repository.GetByIdAsync(result.BookingId);

        savedBooking.Should().NotBeNull();
        savedBooking.CustomerId.Should().Be(1L);
        savedBooking.Status.Should().Be(BookingStatus.Draft);
    }

    [Fact]
    public async Task CreateBooking_WithInactiveService_ShouldThrowException()
    {
        // Arrange
        await SeedInactiveService();

        var command = new CreateBookingCommand(
            CustomerId: 1L,
            ServiceCatalogId: 999L, // Inactive service
            RequestedStartTime: DateTime.UtcNow.AddDays(1),
            EstimatedDuration: TimeSpan.FromHours(2),
            ServiceAddress: CreateTestAddressDto(),
            ContactInfo: CreateTestContactInfoDto(),
            SpecialInstructions: null);

        var handler = GetService<ICommandHandler<CreateBookingCommand, CreateBookingResult>>();

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*inactive service*");
    }
}
```

### Integration Tests - Domain Events

**BookingDomainEventTests.cs**
```csharp
public class BookingDomainEventTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateBooking_ShouldPublishBookingCreatedEvent()
    {
        // Arrange
        var eventHandlerMock = new Mock<INotificationHandler<BookingCreatedEvent>>();
        RegisterService(eventHandlerMock.Object);

        await SeedServiceCatalog();

        var command = CreateValidBookingCommand();
        var handler = GetService<ICommandHandler<CreateBookingCommand, CreateBookingResult>>();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        eventHandlerMock.Verify(
            h => h.Handle(
                It.IsAny<BookingCreatedEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmBooking_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var integrationEventBusMock = new Mock<IBus>();
        RegisterService(integrationEventBusMock.Object);

        var booking = await CreateConfirmedBooking();

        // Act
        // Trigger domain event handler which publishes integration event
        var handler = GetService<INotificationHandler<BookingConfirmedEvent>>();
        await handler.Handle(
            new BookingConfirmedEvent(booking.Id, booking.TechnicianId!.Value, booking.ScheduledTimeSlot),
            CancellationToken.None);

        // Assert
        integrationEventBusMock.Verify(
            bus => bus.Publish(
                It.Is<BookingConfirmedIntegrationEvent>(e =>
                    e.BookingId == booking.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

### Integration Tests - Query Handlers

**GetBookingQueryTests.cs**
```csharp
public class GetBookingQueryTests : IntegrationTestBase
{
    [Fact]
    public async Task GetBooking_WhenExists_ShouldReturnBookingDto()
    {
        // Arrange
        var booking = await SeedBooking();

        var query = new GetBookingQuery(booking.Id);
        var handler = GetService<IQueryHandler<GetBookingQuery, GetBookingResult>>();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Booking.Should().NotBeNull();
        result.Booking.Id.Should().Be(booking.Id);
        result.Booking.CustomerId.Should().Be(booking.CustomerId);
    }

    [Fact]
    public async Task GetBooking_WhenNotExists_ShouldThrowNotFoundException()
    {
        // Arrange
        var query = new GetBookingQuery(999L);
        var handler = GetService<IQueryHandler<GetBookingQuery, GetBookingResult>>();

        // Act
        var act = async () => await handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

### Test Base Class

**IntegrationTestBase.cs**
```csharp
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly MsSqlContainer _sqlContainer;
    protected IServiceProvider _serviceProvider = null!;
    protected FixItDbContext _dbContext = null!;

    protected IntegrationTestBase()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithPassword("TestP@ssw0rd123!")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var services = new ServiceCollection();

        // Add DbContext
        services.AddDbContext<FixItDbContext>(options =>
            options.UseSqlServer(_sqlContainer.GetConnectionString()));

        // Add repositories
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IServiceCatalogRepository, ServiceCatalogRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ITechnicianRepository, TechnicianRepository>();

        // Add MediatR
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(CreateBookingCommand).Assembly));

        // Add FluentValidation
        services.AddValidatorsFromAssembly(typeof(CreateBookingCommand).Assembly);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<FixItDbContext>();

        // Run migrations
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsyncIfSupported();
        await _sqlContainer.DisposeAsync();
    }

    protected T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    protected void RegisterService<T>(T implementation) where T : class
    {
        var services = new ServiceCollection();
        foreach (var service in (_serviceProvider as ServiceProvider)!)
        {
            services.Add(service);
        }
        services.AddSingleton(implementation);
        _serviceProvider = services.BuildServiceProvider();
    }

    // Test data helpers
    protected async Task<ServiceCatalog> SeedServiceCatalog()
    {
        var service = ServiceCatalog.Create(
            "Plumbing Repair",
            "General plumbing repair services",
            ServiceCategory.Plumbing,
            Price.FromAmount(75.00m),
            Duration.FromHours(2),
            new List<TechnicianSkill>());

        await _dbContext.ServiceCatalogs.AddAsync(service);
        await _dbContext.SaveChangesAsync();

        return service;
    }

    protected async Task<Booking> SeedBooking()
    {
        var service = await SeedServiceCatalog();

        var booking = Booking.Create(
            customerId: 1L,
            service: ServiceInfo.FromCatalog(service),
            requestedTimeSlot: TimeSlot.Create(
                DateTime.UtcNow.AddDays(1),
                TimeSpan.FromHours(2)),
            serviceAddress: new Address("123 Main St", "Springfield", "IL", "62701", null, null),
            contactInfo: new ContactInfo("John Doe", "555-1234", null, "john@example.com"));

        await _dbContext.Bookings.AddAsync(booking);
        await _dbContext.SaveChangesAsync();

        return booking;
    }
}
```

---

## 📁 Module Structure

Follow the modular monolith pattern from the existing codebase.

### Directory Structure

```
Modules/
└── FixIt/
    └── FixIt/
        ├── Bookings/
        │   ├── Models/
        │   │   └── Booking.cs (Aggregate)
        │   ├── ValueObjects/
        │   │   ├── BookingDetail.cs
        │   │   ├── BookingStatus.cs
        │   │   └── TimeSlot.cs
        │   ├── Events/
        │   │   ├── BookingCreatedEvent.cs
        │   │   ├── BookingConfirmedEvent.cs
        │   │   └── TechnicianAssignedEvent.cs
        │   ├── EventHandlers/
        │   │   └── BookingCreatedEventHandler.cs
        │   ├── Features/
        │   │   ├── CreateBooking/
        │   │   │   ├── CreateBookingCommand.cs
        │   │   │   ├── CreateBookingCommandHandler.cs
        │   │   │   ├── CreateBookingCommandValidator.cs
        │   │   │   └── CreateBookingEndpoint.cs
        │   │   ├── GetBooking/
        │   │   │   ├── GetBookingQuery.cs
        │   │   │   ├── GetBookingQueryHandler.cs
        │   │   │   └── GetBookingEndpoint.cs
        │   │   └── ...
        │   ├── Specifications/
        │   │   ├── BookingsByCustomerSpecification.cs
        │   │   └── BookingsByStatusSpecification.cs
        │   └── IBookingRepository.cs
        │
        ├── ServiceCatalogs/
        │   ├── Models/
        │   │   └── ServiceCatalog.cs
        │   ├── ValueObjects/
        │   │   └── ServiceCategory.cs
        │   ├── Features/
        │   └── IServiceCatalogRepository.cs
        │
        ├── Customers/
        │   ├── Models/
        │   │   └── Customer.cs
        │   ├── Features/
        │   └── ICustomerRepository.cs
        │
        ├── Technicians/
        │   ├── Models/
        │   │   └── Technician.cs
        │   ├── Features/
        │   └── ITechnicianRepository.cs
        │
        ├── Data/
        │   ├── FixItDbContext.cs
        │   ├── Configurations/
        │   │   ├── BookingConfiguration.cs
        │   │   ├── ServiceCatalogConfiguration.cs
        │   │   └── ...
        │   └── Repository/
        │       ├── BookingRepository.cs
        │       ├── CachedBookingRepository.cs
        │       └── ...
        │
        └── FixItModule.cs (Module registration)
```

### Module Registration

**FixItModule.cs**
```csharp
public static class FixItModule
{
    public static IServiceCollection AddFixItModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<FixItDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"));

            // Add interceptors
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>(),
                serviceProvider.GetRequiredService<DispatchDomainEventInterceptor>());
        });

        // Repositories with caching
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.Decorate<IBookingRepository, CachedBookingRepository>();

        services.AddScoped<IServiceCatalogRepository, ServiceCatalogRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ITechnicianRepository, TechnicianRepository>();

        // MediatR handlers (auto-discovered from assembly)

        // FluentValidation
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IApplicationBuilder UseFixItModule(this IApplicationBuilder app)
    {
        // Apply migrations
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FixItDbContext>();
        dbContext.Database.Migrate();

        return app;
    }
}
```

### DbContext

**FixItDbContext.cs**
```csharp
public class FixItDbContext(DbContextOptions<FixItDbContext> options)
    : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ServiceCatalog> ServiceCatalogs => Set<ServiceCatalog>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Technician> Technicians => Set<Technician>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("fixit");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }
}
```

### Entity Configuration

**BookingConfiguration.cs**
```csharp
public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.CustomerId)
            .IsRequired();

        builder.OwnsOne(b => b.Detail, detail =>
        {
            detail.Property(d => d.ServiceType).HasMaxLength(100).IsRequired();
            detail.Property(d => d.Description).HasMaxLength(500);
            detail.Property(d => d.SpecialInstructions).HasMaxLength(1000);

            detail.OwnsOne(d => d.ServiceAddress, address =>
            {
                address.Property(a => a.Street).HasMaxLength(200);
                address.Property(a => a.City).HasMaxLength(100);
                address.Property(a => a.State).HasMaxLength(50);
                address.Property(a => a.PostalCode).HasMaxLength(20);
            });

            detail.OwnsOne(d => d.ContactInfo, contact =>
            {
                contact.Property(c => c.Name).HasMaxLength(200);
                contact.Property(c => c.Phone).HasMaxLength(20);
                contact.Property(c => c.Email).HasMaxLength(200);
            });
        });

        builder.OwnsOne(b => b.ScheduledTimeSlot, slot =>
        {
            slot.Property(s => s.StartTime).IsRequired();
            slot.Property(s => s.EndTime).IsRequired();
        });

        builder.OwnsOne(b => b.EstimatedPrice, price =>
        {
            price.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            price.Property(p => p.Currency).HasMaxLength(3);
        });

        // Ignore domain events (not persisted)
        builder.Ignore(b => b.DomainEvents);

        // Indexes
        builder.HasIndex(b => b.CustomerId);
        builder.HasIndex(b => b.TechnicianId);
        builder.HasIndex(b => b.Status);
    }
}
```

### Carter Endpoint

**CreateBookingEndpoint.cs**
```csharp
public class CreateBookingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/bookings", async (
            CreateBookingCommand command,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(command, cancellationToken);
            return Results.Created($"/bookings/{result.BookingId}", result);
        })
        .WithName("CreateBooking")
        .WithTags("Bookings")
        .WithSummary("Create a new booking")
        .WithDescription("Creates a new booking in draft status")
        .Produces<CreateBookingResult>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
```

---

## 🎁 Bonus Challenges (Optional)

### 1. Recurring Bookings
Implement recurring booking patterns (daily, weekly, monthly).

**Additional Requirements:**
- `RecurringBooking` aggregate that generates individual bookings
- `RecurrencePattern` value object (frequency, interval, end date)
- Business rules for recurring booking creation and cancellation
- Handle conflicts when generating future bookings

### 2. Service Packages
Bundle multiple services together with discounted pricing.

**Additional Requirements:**
- `ServicePackage` aggregate containing multiple services
- Package pricing rules (percentage discount, fixed discount)
- Validation that all services in package are available
- Booking multiple services at once

### 3. Dynamic Pricing
Adjust pricing based on demand, time of day, urgency.

**Additional Requirements:**
- `PricingStrategy` interface with implementations
- `UrgentBookingPricingStrategy` (surcharge for same-day bookings)
- `PeakHoursPricingStrategy` (higher rates during peak hours)
- `SeasonalPricingStrategy` (adjust by season/holidays)

### 4. Multi-Technician Bookings
Services that require multiple technicians working together.

**Additional Requirements:**
- Extend `Booking` to support multiple technician assignments
- Validation that all technicians are available
- Coordination logic for multi-technician scheduling
- Split payment/credit between technicians

### 5. Customer Ratings & Reviews
Allow customers to rate and review completed services.

**Additional Requirements:**
- `BookingReview` entity with rating, comment, photos
- Business rules: only completed bookings can be reviewed
- Aggregate technician ratings
- Display average ratings in technician queries

---

## ✅ Evaluation Criteria

Your implementation will be evaluated on:

### Domain Model Design (30%)
- [ ] Proper aggregate boundaries and responsibilities
- [ ] Rich domain models with encapsulated business logic
- [ ] Correct use of entities vs value objects
- [ ] Business rules enforced within aggregates
- [ ] Domain events used appropriately

### CQRS Implementation (20%)
- [ ] Clear separation between commands and queries
- [ ] Command handlers modify state through aggregates
- [ ] Query handlers optimized for read operations
- [ ] Validation using FluentValidation
- [ ] Proper use of MediatR pipeline

### Repository Pattern (15%)
- [ ] Repository interfaces abstraction over data access
- [ ] Specification pattern for complex queries
- [ ] Caching decorator pattern implementation
- [ ] No IQueryable leaking from repositories

### Domain Events (15%)
- [ ] Domain events raised for important state changes
- [ ] Event handlers implement cross-aggregate logic
- [ ] Integration events for module communication
- [ ] Event dispatch mechanism properly configured

### Testing (15%)
- [ ] Comprehensive unit tests for aggregates
- [ ] Value object validation tests
- [ ] Integration tests for handlers
- [ ] Domain event tests
- [ ] Test coverage >80%

### Code Organization (5%)
- [ ] Follows modular monolith structure
- [ ] Clear folder organization
- [ ] Consistent naming conventions
- [ ] Proper use of namespaces

---

## 📚 Reference Examples from Codebase

### Key Files to Reference

1. **Aggregate Pattern**: `Modules/Request/Request/Requests/Models/Request.cs`
2. **Value Objects**: `Modules/Request/Request/Requests/ValueObjects/RequestStatus.cs`
3. **Domain Events**: `Modules/Request/Request/Requests/Events/RequestCreatedEvent.cs`
4. **Command Handler**: `Modules/Request/Request/Requests/Features/CreateRequest/CreateRequestCommandHandler.cs`
5. **Query Handler**: `Modules/Request/Request/Requests/Features/GetRequests/GetRequestQueryHandler.cs`
6. **Repository**: `Modules/Request/Request/Data/Repository/RequestRepository.cs`
7. **Caching Decorator**: `Modules/Request/Request/Data/Repository/CachedRequestRepository.cs`
8. **Specification**: `Modules/Request/Request/Requests/Specifications/RequestsByStatusSpecification.cs`
9. **DbContext**: `Modules/Request/Request/Data/RequestDbContext.cs`
10. **Module Registration**: `Modules/Request/Request/RequestModule.cs`

### Patterns to Follow

**From Request Aggregate:**
- Factory methods for creation
- Private setters for properties
- Business rule validation with `RuleCheck`
- Domain event raising with `AddDomainEvent`
- Collection encapsulation with readonly lists

**From Event Handling:**
- Automatic dispatch via `DispatchDomainEventInterceptor`
- Translation to integration events in event handlers
- Publishing through MassTransit

**From Repository Pattern:**
- Interface-based abstraction
- Decorator pattern for caching
- Specification pattern for queries

---

## 🚀 Getting Started

### Step 1: Set Up Module Structure
1. Create the `Modules/FixIt/FixIt` directory
2. Set up the basic folder structure (Models, Features, Data, etc.)
3. Create `FixItModule.cs` for module registration

### Step 2: Implement Core Aggregates
1. Start with `Booking` aggregate
2. Implement required value objects (BookingDetail, TimeSlot, etc.)
3. Add business methods and validation

### Step 3: Implement Repository Layer
1. Create `IBookingRepository` interface
2. Implement `BookingRepository`
3. Create `FixItDbContext`
4. Configure entity mappings

### Step 4: Implement Commands
1. Create `CreateBookingCommand` and handler
2. Add validation with FluentValidation
3. Implement other booking commands

### Step 5: Implement Queries
1. Create `GetBookingQuery` and handler
2. Implement paginated queries
3. Add filtering with specifications

### Step 6: Add Domain Events
1. Define domain event records
2. Implement event handlers
3. Configure event dispatcher

### Step 7: Test Everything
1. Write unit tests for aggregates
2. Write integration tests for handlers
3. Ensure >80% coverage

### Step 8: Additional Features
1. Implement remaining aggregates (ServiceCatalog, Customer, Technician)
2. Add scheduling features
3. Complete all four core features

---

## 💡 Tips for Success

1. **Start Simple**: Begin with the core `Booking` aggregate and basic CRUD operations, then add complexity.

2. **Follow Existing Patterns**: Reference the `Request` module extensively - it's your blueprint.

3. **Test Early**: Write tests as you go, not at the end. Tests help validate your domain design.

4. **Think in Business Terms**: Use ubiquitous language. Methods like `Confirm()` instead of `SetStatus("Confirmed")`.

5. **Encapsulate Everything**: No public setters. All state changes go through business methods.

6. **Domain Events Are Key**: Raise events for every important state change. They enable loose coupling.

7. **Keep Aggregates Small**: Don't try to model everything in one aggregate. Use references (IDs) between aggregates.

8. **Validate at Boundaries**: Use FluentValidation for command validation, RuleCheck for domain rules.

9. **Specifications for Queries**: Don't let complex query logic leak into repositories or handlers.

10. **Don't Skip Tests**: This is a learning exercise - tests demonstrate you understand the patterns.

---

## 📖 Learning Resources

### Domain-Driven Design
- **Book**: "Domain-Driven Design" by Eric Evans
- **Book**: "Implementing Domain-Driven Design" by Vaughn Vernon
- **Reference**: `docs/DDD.md` in this repository

### CQRS Pattern
- **Article**: Microsoft CQRS Pattern documentation
- **Reference**: Command and Query handlers in `Modules/Request`

### Testing
- **Framework**: xUnit documentation
- **Library**: FluentAssertions for readable assertions
- **Library**: Testcontainers for integration tests

---

## 📝 Submission Guidelines

When complete, your solution should include:

1. ✅ All four core features implemented
2. ✅ Complete domain model with aggregates and value objects
3. ✅ CQRS commands and queries with handlers
4. ✅ Repository pattern with specifications
5. ✅ Domain events and handlers
6. ✅ Comprehensive test suite (unit + integration)
7. ✅ Database migrations
8. ✅ Module registration and configuration
9. ✅ Carter endpoints for all operations
10. ✅ README documenting your design decisions

### Bonus Points For:
- Implementing one or more bonus challenges
- Excellent test coverage (>90%)
- Clean, well-organized code
- Thoughtful domain model design
- Performance optimizations (caching, query optimization)

---

## ❓ FAQ

**Q: Should I use EF Core or another ORM?**
A: Use EF Core to match the existing codebase.

**Q: How detailed should my value objects be?**
A: Every value object should have validation and be immutable. Use records for simple VOs.

**Q: Do I need to implement authentication?**
A: No, focus on domain logic and CQRS. Authentication is handled at the API gateway level.

**Q: Should I use integration events or domain events?**
A: Both. Domain events for intra-aggregate communication, integration events for inter-module communication.

**Q: How do I test domain events?**
A: Mock the IMediator or INotificationHandler in tests, or use integration tests with the full pipeline.

**Q: Can I modify the existing shared code?**
A: Yes, if you need additional base classes or utilities. Just document your changes.

---

## 🎉 Good Luck!

This challenge is designed to give you hands-on experience with DDD and CQRS in a realistic scenario. Take your time, follow the patterns, and don't hesitate to reference the existing `Request` module when you're stuck.

Remember: **The goal is learning, not perfection.** Focus on understanding the patterns and principles rather than building a production-ready system.

Happy coding! 🚀

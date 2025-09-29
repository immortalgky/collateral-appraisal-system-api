# Outbox/Inbox Pattern Usage Guide

This guide explains how to adopt the shared Outbox/Inbox pattern found in `Shared.Messaging.OutboxPatterns`. The `Request` module acts as a reference implementation you can mirror when wiring a new bounded context.

---

## Why an Outbox/Inbox pattern?

Robust messaging requires that domain state and published integration events stay consistent. The pattern in this repository achieves that by:

- Recording integration events in an **Outbox** table inside the same transaction as your domain aggregates.
- Using Quartz-hosted jobs to read and publish those events through MassTransit.
- Recording inbound messages in an **Inbox** table, providing idempotency and duplicate detection before executing application logic.

You gain reliable once-only delivery semantics without distributed transactions.

---

## Prerequisites

Before adopting the shared pattern in a module, ensure these services are already available:

- **Entity Framework Core** with SQL Server (or the provider your DbContext uses).
- **MassTransit** (configured elsewhere in the solution) because the outbox publishes with `IPublishEndpoint`.
- **Quartz** background scheduling (added automatically by the extension but requires the hosting app to allow hosted services).
- `ISqlConnectionFactory` and the other shared infrastructure provided by the `Shared` projects.
- Domain events that implement `IExternalized<T>` and expose `ToIntegrationEvent()`; the interceptor translates them into integration events.

For a deep dive into internals, see `docs/Outbox_Pattern_Deep_Dive.md`.

---

## Step 1 – Prepare your DbContext

Add both inbox and outbox support inside `OnModelCreating`. The `RequestDbContext` shows the minimal implementation:

```csharp
// For Outbox table
modelBuilder.AddOutboxSupport("request");

// For Inbox table
modelBuilder.AddInboxSupport("request");
```

Key points:

- Use a schema name that matches where your module stores tables. The same schema string is reused when registering services.
- `AddOutboxSupport` and `AddInboxSupport` apply entity configurations so EF includes the `OutboxMessages` and `InboxMessages` tables in migrations.

> ✅ **Remember to add a migration** (for example, `dotnet ef migrations add AddRequestOutboxInbox`) after changing the model and apply it to the database.

---

## Step 2 – Capture domain events during `SaveChanges`

Outbox rows are created by the `DispatchDomainEventInterceptor` in `Shared.Data.Interceptors`. Register it (and any audit interceptors) with your module:

```csharp
services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();
```

With the interceptor in place, each aggregate that raises an externalized domain event enqueues the translated integration event into the outbox table inside the same transaction as your entity updates.

---

## Step 3 – Register the DbContext and seed data

Set up the DbContext as usual. `RequestModule` configures SQL Server, applies the interceptors, and adds a data seeder:

```csharp
services.AddDbContext<RequestDbContext>((sp, options) =>
{
    options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
    options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
    {
        sqlOptions.MigrationsAssembly(typeof(RequestDbContext).Assembly.GetName().Name);
        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "request");
    });
});

services.AddScoped<IDataSeeder<RequestDbContext>, RequestDataSeed>();
```

Follow the same structure for other modules, swapping in the module-specific context, schema, and seeder.

---

## Step 4 – Wire the Outbox and Inbox services

Register the shared messaging services with the provided extension methods:

```csharp
// Outbox – publishes integration events
services.AddOutbox<RequestDbContext>(configuration, "request");

// Inbox – ensures idempotent consumer execution
services.AddInbox<RequestDbContext>(
    configuration,
    typeof(RequestModule).Assembly,
    "request");
```

What the extensions do for you:

- Register keyed repositories and services scoped to the DbContext schema so multiple modules can coexist.
- Add Quartz jobs: `OutboxProcessorJob<TDbContext>` publishes stored events with `IPublishEndpoint`, and `InboxCleanupJob<TDbContext>` deletes expired inbox messages.
- Scan the provided assembly for MassTransit `IConsumer<T>` implementations, wrapping them with `ConsumeWrapper<TMessage, TConsumer>` to perform duplicate checks and inbox persistence automatically.

If a module does **not** need inbound processing, omit `AddInbox`; similarly, modules that only receive events can skip `AddOutbox`.

---

## Step 5 – Configure background jobs

Add the required configuration in `appsettings.[Environment].json`. The sample below mirrors the defaults in `Bootstrapper/Api`:

```json
"Jobs": {
  "RetentionDays": 7,
  "InboxCleanup": {
    "CronExpression": "0 0 0 * * ?"
  },
  "OutboxProcessor": {
    "CronExpression": "0/5 * * * * ?",
    "BatchSize": 50,
    "Chunk": 5
  }
}
```

| Setting | Purpose | Notes |
| --- | --- | --- |
| `Jobs:RetentionDays` | Days to keep inbox rows before cleanup. | Adjust to match compliance requirements. |
| `Jobs:InboxCleanup:CronExpression` | Quartz schedule for `InboxCleanupJob`. | Defaults to deleting once per day. |
| `Jobs:OutboxProcessor:CronExpression` | Schedule for pulling and publishing pending outbox messages. | Default runs every 5 seconds. |
| `Jobs:OutboxProcessor:BatchSize` | Max messages fetched per database query. | Align with expected throughput. |
| `Jobs:OutboxProcessor:Chunk` | Sub-batch size processed between `SaveChanges` calls. | Reduces transaction size and helps retry logic. |

Ensure these sections exist in the configuration source consumed by your hosting application (environment variables, KeyVault, etc.).

---

## Step 6 – Initialize the module at startup

Once the services are registered, activate the module within the API host. In `Program.cs`, call the module extension methods so migrations run and Quartz hosted services start:

```csharp
builder.Services.AddRequestModule(builder.Configuration);
// ... other modules

var app = builder.Build();

app.UseRequestModule();
```

`UseRequestModule` applies pending migrations for `RequestDbContext`, ensuring the inbox/outbox tables exist before jobs execute.

---

## Working with the Inbox in consumers

The shared infrastructure wraps each MassTransit consumer class in a `ConsumeWrapper`. When a message arrives:

1. The wrapper resolves `IInboxService` keyed by schema.
2. `CheckDuplicate` verifies the message `EventId` has not already been processed.
3. The message is persisted to the inbox table for auditing and deduplication.
4. Your consumer logic executes.

To participate, ensure your integration events expose `EventId` (Guid) and `OccurredOn` (DateTime) properties—`InboxService.AddMessageInboxAsync` relies on them.

---

## Monitoring and troubleshooting

- **Job logging:** Quartz jobs log execution times and warnings; monitor application logs for entries from `OutboxProcessorJob` and `InboxCleanupJob`.
- **Failed publishes:** `OutboxMessage` rows track `RetryCount`, `MaxRetries`, and `ExceptionInfo`. Investigate messages that exceed retry limits.
- **Duplicate errors:** `InboxService.CheckDuplicate` throws a `NotFoundException` when a duplicate is detected. Adjust retention or consumer behavior if you see unexpected duplicates.
- **Scaling:** Multiple instances of the API can run safely—Quartz jobs execute on each node, but outbox rows are processed transactionally, so only one instance removes a given row.

---

## Checklist for new modules

1. Add inbox/outbox support to your DbContext with the correct schema.
2. Register the shared save-change interceptors.
3. Configure your DbContext and seeders.
4. Call `AddOutbox<TDbContext>` / `AddInbox<TDbContext>` in your module setup.
5. Provide job configuration values.
6. Expose module `Add`/`Use` extensions in the hosting application.

Following the `RequestModule` blueprint ensures your module gains reliable message publication and consumption with minimal custom code.

# Workflow Persistence & Transaction Best Practices

Here’s a practical, battle-tested way to persist workflow state for a long-running/orchestrating module (maker→checker, human tasks, external IO, auto-complete, etc.). The key is to make every *state change* an atomic, idempotent step and to decouple side-effects from the state machine.

---

## 1) Core persistence model (tables/entities)
- **WorkflowInstance**: `Id, DefinitionId, Version, Status, CorrelationId, Data(Json), CurrentActivityId, ConcurrencyToken(RowVersion), UpdatedAt`.
- **ActivityInstance**: `Id, WorkflowInstanceId, ActivityId, State(Json), Status, StartedAt, CompletedAt, ConcurrencyToken`.
- **Bookmark / WaitHandle** (for user or external events): `Id, WorkflowInstanceId, ActivityId, Type(UserAction|Timer|Message), Key, Payload(Json), IsConsumed`.
- **ExecutionLog / Checkpoint**: append-only audit of transitions: `Id, WorkflowInstanceId, ActivityId, Event(Started|Completed|Faulted|Suspended|Resumed), At, Details(Json)`.
- **Outbox** (for integration events/notifications): `Id, OccurredAt, Type, Payload, Headers, Attempts, NextAttemptAt, Status`.
- (Optional) **Inbox** for exactly-once message handling if you consume bus messages.

---

## 2) Transaction boundaries (golden rules)
Think in **steps**. Each step changes at most one activity status and the workflow pointer. One step = one transaction.

### A. Pure state transition (no external decision needed)
- Examples: move from “Assigned” → “InProgress”, set due date, create bookmark.
- **Inside a single DB transaction**:
    1) Load instance with *optimistic concurrency* (rowversion).
    2) Validate guards (mandatory fields, role checks) using current DB state/read models.
    3) Apply transition (mutate `WorkflowInstance`, `ActivityInstance`, create or consume `Bookmark`).
    4) Write **ExecutionLog** row.
    5) Write **Outbox** messages (e.g., “TaskAssigned”, “StatusChanged”).
    6) `SaveChanges()` → **COMMIT**.
- **After commit (out of tx)**: the outbox dispatcher publishes messages. No further state changes.

### B. Transition whose *result depends on external IO* (HTTP, file store, scoring service)
- Do **not** hold a DB tx while waiting on the network.
- Two safe patterns:

#### B1. Two-phase (recommend):
- Tx #1 (short): record an *intent*:
    - Add `PendingExternalCall` to activity state (payload + idempotency key).
    - Add an outbox “PerformExternalCall” command for a background worker.
    - Commit.
- Worker performs the external call with retries/timeouts.
- Tx #2 (short): consume the command result (success/failure), apply the final transition, log, enqueue next outbox events, commit.

#### B2. Synchronous but reversible (only when latency is tiny):
- Execute external call **first** with an idempotency key.
- Start a short tx to apply state based on the response; if commit fails due to concurrency, **retry** step using the same idempotency key (so the external call won’t duplicate).

### C. Human interaction (user completes/approves/edits)
- Treat the button click as a **Resume** command:
    - Tx:
        1) Lock by rowversion (or `(WHERE Id = … AND RowVersion = …)`).
        2) Re-validate mandatory fields/guards *again* at resume time.
        3) Consume the corresponding `Bookmark` (set `IsConsumed = 1`).
        4) Transition activity to `Completed` (or `Rejected`), move pointer, write log, enqueue outbox.
        5) Commit.
- If the UI posts twice, the bookmark consumption + rowversion makes the operation **idempotent** (2nd try finds bookmark already consumed → no-op).

---

## 3) When exactly to persist / checkpoint
- **Always** persist on:
    - Entering an activity (Started).
    - Creating any wait/timeout condition (Bookmark/Timer).
    - Completing/Rejecting an activity.
    - Scheduling any external call (record the intent).
    - Catching a fault (store fault info and mark instance `Suspended`).
- **Do not** wait to “batch many changes at the end” for long-running flows—checkpoint **every step**. It improves recoverability and operator visibility.

---

## 4) Concurrency & locking
- Prefer **optimistic concurrency** (rowversion) on `WorkflowInstance` and `ActivityInstance`.
- Use short, index-friendly queries; avoid table/scan locks.
- If you must serialize assignment (e.g., round-robin “pick next 3 tasks”), do it in a **stored proc** with `UPDLOCK, HOLDLOCK` (or `SERIALIZABLE`) on a **narrow index** and commit immediately after reserving/marking tasks. Then process them in separate steps. Keep the critical section tiny.

---

## 5) Guards & validation placement
- Put **guard checks** (mandatory fields, role, data completeness) **inside the same tx** that performs the transition so the decision and state change are atomic.
- If a guard needs cross-module data, use:
    - a **read model / view table** (periodically or event-driven synced), or
    - a domain service query (read-only) but **never** hold a tx while calling another module’s DB. Validate, then open tx to mutate.
- If guard depends on an external system, use the **two-phase** pattern (B1): cache the external fact (with TTL) and make the commit a pure state mutation.

---

## 6) Outbox & side-effects
- Every transition that should notify other modules or send emails/SignalR should write an **Outbox** row **inside the same tx** as the state change.
- A background **OutboxDispatcher** (HostedService) publishes and marks sent. Retries with exponential backoff. This guarantees “state first, effects later”.

---

## 7) Timers / auto-complete / SLAs
- Represent timers as bookmarks: `Type = Timer, Key = <activityId>, DueAt`.
- A **TimerSweeper** reads due timers, enqueues “Resume” commands (idempotent), and the normal resume step consumes the bookmark inside a tx.
- Auto-complete is just another resume path run by a worker.

---

## 8) Faults & retries
- If a step faults:
    - **Inside tx**: write ExecutionLog with the exception; set `Status = Suspended` (or increment a retry counter in activity state); commit.
    - A **Compensation** or **RetryPolicy** worker can later resume or route to a “Manual Intervention” activity.
- External calls: keep **idempotency keys** and **last attempt** data in the activity state so retries are safe.

---

## 9) Sample step skeleton (EF Core / MediatR style)
```csharp
public sealed record ResumeCommand(Guid WorkflowId, string BookmarkId, string actorId) : IRequest;

public sealed class ResumeHandler : IRequestHandler<ResumeCommand>
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IOutbox _outbox;

    public async Task Handle(ResumeCommand req, CancellationToken ct)
    {
        // Load with concurrency token
        var wf = await _db.WorkflowInstances
            .Include(x => x.Activities)
            .FirstOrDefaultAsync(x => x.Id == req.WorkflowId, ct)
            ?? throw new NotFoundException();

        // Guard: bookmark must exist and not consumed
        var bm = await _db.Bookmarks.FirstOrDefaultAsync(
            x => x.WorkflowInstanceId == req.WorkflowId && x.Id == req.BookmarkId && !x.IsConsumed, ct)
            ?? throw new InvalidOpException("Bookmark not available");

        // Apply transition
        bm.IsConsumed = true;

        var act = wf.Activities.Single(a => a.Id == bm.ActivityId);
        act.Status = ActivityStatus.Completed;
        act.CompletedAt = _clock.UtcNow;

        wf.CurrentActivityId = NextPointer(wf, act);
        wf.UpdatedAt = _clock.UtcNow;

        _db.ExecutionLogs.Add(new ExecutionLog { /* ... */ });

        await _outbox.EnqueueAsync(new TaskCompletedEvent(/*…*/ ), ct);

        // Atomic commit
        await _db.SaveChangesAsync(ct);
        // Outbox dispatcher publishes later
    }
}
```

---

## 10) Practical “when to commit” checklist
- **Before** calling a slow/uncertain external dependency → commit a *plan/intent* (and trigger worker).
- **After** deterministic local work → commit immediately (no extra waits).
- **On user actions** → validate + transition + log + outbox, then commit (one round trip).
- **On timers** → the timer only schedules the resume; the resume step commits the real state change.

---

## 11) Observability hooks (helps debugging & ops)
- Correlate every step with `TraceId / CorrelationId`.
- Record `ExecutionLog` (step events, duration, actor, input).
- Emit metrics: “active instances, suspended, avg step duration, retries”.
- Store last error on `ActivityInstance` for quick diagnosis.

---

## TL;DR (rules you can adopt tomorrow)
1) **One step, one transaction.**
2) **Never hold DB transactions across network or UI waits.**
3) **Use outbox for all effects; publish after commit.**
4) **Checkpoint on every transition (start/wait/complete/fault).**
5) **Optimistic concurrency with rowversion; keep critical sections tiny.**
6) **External dependencies → two-phase: persist intent, then perform, then finalize.**
7) **Bookmarks for waits (user/timer/message); consume them atomically on resume.**

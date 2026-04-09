# Activity Menu Permissions — Design & Implementation Plan

## Overview

Add an **activity-scoped menu permission system** to the Workflow module. Each workflow activity (in the `WorkflowDefinition.JsonDefinition`) declares which UI menus/sections are accessible and at what level: **Edit**, **ReadOnly**, or **Hidden**.

A new API endpoint returns these permissions for a given activity, so the frontend can dynamically show/hide and enable/disable UI sections per workflow step.

---

## Design Decisions

| Decision | Alternatives Considered | Why This |
|----------|------------------------|----------|
| Activity-scoped visibility (not RBAC) | Role-based, combined | Core need: different activities show different menus |
| Config inside WorkflowDefinition JSON | Database table, config file | Versioned with workflow; no extra tables |
| Three access levels (Edit/ReadOnly/Hidden) | Two or four levels | Covers all UI states without over-complication |
| Separate endpoint (not embedded in task response) | Embedded, both | Clean separation; frontend calls when needed |
| Generic string keys (no hardcoded enum) | Hardcoded enum of menus | Frontend decides; backend stays decoupled |
| Strongly-typed C# models | Direct JSON parsing, service-only | Compile-time safety, IntelliSense, clear contracts |
| Exclude Hidden items from response | Include all items | Less data over the wire; unlisted = hidden by default |
| IMemoryCache with definition-versioned keys | No caching, Redis | Fast reads; invalidated on definition update |

---

## JSON Schema Extension

Each activity in `WorkflowDefinition.JsonDefinition` gets a `menus` array:

```json
{
  "activities": [
    {
      "id": "appraisal-staff",
      "name": "Appraisal Staff Review",
      "type": "HumanTask",
      "menus": [
        { "menuKey": "PropertyDetails", "accessLevel": "Edit" },
        { "menuKey": "MarketComparables", "accessLevel": "Edit" },
        { "menuKey": "UploadPhotos", "accessLevel": "Edit" },
        { "menuKey": "PricingAnalysis", "accessLevel": "Hidden" },
        { "menuKey": "FinalReport", "accessLevel": "Hidden" }
      ]
    },
    {
      "id": "appraisal-checker",
      "name": "Appraisal Checker Review",
      "type": "HumanTask",
      "menus": [
        { "menuKey": "PropertyDetails", "accessLevel": "ReadOnly" },
        { "menuKey": "MarketComparables", "accessLevel": "ReadOnly" },
        { "menuKey": "PricingAnalysis", "accessLevel": "Edit" },
        { "menuKey": "FinalReport", "accessLevel": "Hidden" }
      ]
    }
  ]
}
```

---

## API Contract

```
GET /api/workflow/activities/{activityId}/menus
```

**Response (200 OK)**:
```json
{
  "activityId": "appraisal-staff",
  "menus": [
    { "menuKey": "PropertyDetails", "accessLevel": "Edit" },
    { "menuKey": "MarketComparables", "accessLevel": "Edit" },
    { "menuKey": "UploadPhotos", "accessLevel": "Edit" }
  ]
}
```

- Hidden items are **excluded** from the response (unlisted = hidden by default)
- Returns 404 if activity not found in active workflow definition

---

## Data Flow

```
Frontend -> GET /activities/{activityId}/menus
          -> Carter Endpoint
          -> MediatR: GetActivityMenusQuery
          -> GetActivityMenusQueryHandler
          -> IActivityMenuService.GetMenusForActivityAsync()
               |-- Cache hit? -> return cached ActivityMenuConfig
               |-- Cache miss? -> Load WorkflowDefinition from DB
                               -> Deserialize JsonDefinition
                               -> Map to Dictionary<activityId, ActivityMenuConfig>
                               -> Cache it
                               -> Return requested config
          -> Map to GetActivityMenusResponse (exclude Hidden items)
          -> 200 OK with JSON
```

---

## New Files

| # | File Path (relative to Modules/Workflow/Workflow/) | Purpose |
|---|---------------------------------------------------|---------|
| 1 | `Workflow/Models/MenuAccessLevel.cs` | Enum: Edit, ReadOnly, Hidden |
| 2 | `Workflow/Models/ActivityMenuPermission.cs` | Record: single menu item permission |
| 3 | `Workflow/Models/ActivityMenuConfig.cs` | Record: all menus for one activity |
| 4 | `Workflow/Services/IActivityMenuService.cs` | Interface for menu resolution |
| 5 | `Workflow/Services/ActivityMenuService.cs` | Implementation: JSON parsing + caching |
| 6 | `Workflow/Features/GetActivityMenus/GetActivityMenusEndpoint.cs` | Carter endpoint |
| 7 | `Workflow/Features/GetActivityMenus/GetActivityMenusQuery.cs` | MediatR query + response DTOs |
| 8 | `Workflow/Features/GetActivityMenus/GetActivityMenusQueryHandler.cs` | Query handler |

**Modified files**: `WorkflowModule.cs` (register IActivityMenuService)

---

## Implementation Checklist

### Step 1: Models
- [ ] Create `MenuAccessLevel.cs` enum (Hidden, ReadOnly, Edit)
- [ ] Create `ActivityMenuPermission.cs` record (MenuKey, AccessLevel)
- [ ] Create `ActivityMenuConfig.cs` record (ActivityId, IReadOnlyList<ActivityMenuPermission> Menus)

### Step 2: Service Layer
- [ ] Create `IActivityMenuService.cs` interface with `GetMenusForActivityAsync(string activityId, CancellationToken ct)`
- [ ] Create `ActivityMenuService.cs` implementation
  - [ ] Inject `WorkflowDbContext` and `IMemoryCache`
  - [ ] Load active `WorkflowDefinition` (latest active version)
  - [ ] Deserialize `JsonDefinition` and extract `menus` arrays from activities
  - [ ] Map to `Dictionary<string, ActivityMenuConfig>` and cache
  - [ ] Return `ActivityMenuConfig?` for requested activityId

### Step 3: Query & Handler
- [ ] Create `GetActivityMenusQuery.cs` with query record and response DTOs
  - [ ] `GetActivityMenusQuery(string ActivityId)` implementing `IQuery<GetActivityMenusResponse>`
  - [ ] `GetActivityMenusResponse(string ActivityId, IReadOnlyList<MenuPermissionDto> Menus)`
  - [ ] `MenuPermissionDto(string MenuKey, string AccessLevel)`
- [ ] Create `GetActivityMenusQueryHandler.cs`
  - [ ] Inject `IActivityMenuService`
  - [ ] Call service, map `ActivityMenuConfig` -> response DTO
  - [ ] Filter out Hidden items (only return Edit and ReadOnly)
  - [ ] Throw `NotFoundException` if activity not found

### Step 4: Endpoint
- [ ] Create `GetActivityMenusEndpoint.cs` as Carter module
  - [ ] Route: `GET /api/workflow/activities/{activityId}/menus`
  - [ ] Send `GetActivityMenusQuery` via MediatR
  - [ ] Return 200 with response

### Step 5: Registration
- [ ] Register `IActivityMenuService` / `ActivityMenuService` in `WorkflowModule.cs`

### Step 6: Cache Invalidation
- [ ] In `CreateWorkflowDefinition` handler (or wherever definitions are updated), evict the menu cache key

### Step 7: Verification
- [ ] Build the solution (`dotnet build`)
- [ ] Verify endpoint returns expected response format
- [ ] Verify cache invalidation works on definition update

---

## Assumptions

1. There is only **one active WorkflowDefinition** at a time (latest active version)
2. The `activityId` parameter maps directly to an activity `id` in the JSON definition
3. The frontend already knows what each menu key means and how to render it
4. Menu permissions are **static per activity** — they don't change based on specific appraisal data
5. If an activity has no `menus` array, the endpoint returns an empty list

---

## Non-Goals

- No role-based access control (RBAC) — purely activity-scoped
- No backend enforcement of edit vs readonly (frontend controls UI behavior)
- No admin UI for editing menu configs (managed through workflow definition JSON)
- No hardcoding of specific menu keys in backend code

---

## Review

_To be filled after implementation._

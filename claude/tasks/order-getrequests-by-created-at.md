# Order GetRequests by CreatedAt Descending

## Plan
- [x] Add `.OrderByDescending(r => r.CreatedAt)` to `GetRequestQueryHandler.cs`
- [x] Verify build passes

## Review
**File changed:** `Modules/Request/Request/Application/Features/Requests/GetRequests/GetRequestQueryHandler.cs`

**Change:** Added `.OrderByDescending(r => r.CreatedAt)` between the `.Where()` filter and `.Select()` projection.

**How it works:** EF Core translates this to `ORDER BY CreatedAt DESC` in the SQL query, ensuring newest requests appear first in paginated results. The ordering is applied before pagination (`ToPaginatedResultAsync`), so pages are consistently ordered.

**Impact:** One line added. No other files affected.

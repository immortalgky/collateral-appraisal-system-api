# Global Search API Specification

## Overview

Unified search endpoint that allows the frontend to search across multiple entity types (Requests, Customers, Properties) with a single API call.

---

## Endpoint

```
GET /api/search
```

### Authentication
- Requires Bearer token (existing auth interceptor)

### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `q` | string | Yes | — | Search query string (min 2 characters) |
| `filter` | string | No | `all` | Category filter: `all`, `requests`, `customers`, `properties` |
| `limit` | integer | No | `5` | Max results per category (range: 1–20) |

### Validation Rules
- `q` must be at least 2 characters after trimming; return 400 if shorter
- `filter` must be one of the allowed values; return 400 if invalid
- `limit` must be between 1 and 20; clamp or return 400 if out of range

---

## Response

### Success (200 OK)

```json
{
  "results": {
    "requests": [],
    "customers": [],
    "properties": []
  },
  "totalCount": 0
}
```

### Result Item Schema

Each item in the category arrays follows this structure:

```json
{
  "id": "string",
  "title": "string",
  "subtitle": "string",
  "status": "string | null",
  "category": "requests | customers | properties",
  "navigateTo": "string",
  "icon": "string | null",
  "metadata": {}
}
```

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Entity primary key |
| `title` | string | Primary display text |
| `subtitle` | string | Secondary display text |
| `status` | string? | Current status (e.g., `pending`, `approved`, `active`) |
| `category` | string | Which category this result belongs to |
| `navigateTo` | string | Frontend route path for full page navigation |
| `icon` | string? | Optional icon override (FontAwesome icon name) |
| `metadata` | object | Key-value pairs for preview display (see below) |

---

## Category-Specific Mapping

### Requests

**Search fields**: request number, customer name, status

**Response mapping**:

| Field | Source |
|-------|--------|
| `title` | Request number (e.g., `REQ-2024-001`) |
| `subtitle` | Customer name |
| `status` | Request status |
| `navigateTo` | `/requests/{requestId}` |
| `metadata.requestNumber` | Request number |
| `metadata.status` | Status display value |
| `metadata.customerName` | Customer full name |
| `metadata.collateralType` | Collateral type label |
| `metadata.appraisalPurpose` | Appraisal purpose label |
| `metadata.createdDate` | Created date (formatted: `YYYY-MM-DD`) |
| `metadata.assignedStaff` | Assigned staff full name |

### Customers

**Search fields**: customer name, customer ID, company name

**Response mapping**:

| Field | Source |
|-------|--------|
| `title` | Customer full name |
| `subtitle` | Company name (or `"Individual"` if none) |
| `status` | `active` / `inactive` |
| `navigateTo` | `/customers/{customerId}` |
| `metadata.customerName` | Full name |
| `metadata.customerId` | Customer ID |
| `metadata.companyName` | Company name |
| `metadata.phone` | Phone number |
| `metadata.email` | Email address |
| `metadata.linkedRequestCount` | Count of linked requests (as string) |

### Properties

**Search fields**: address, title deed number, property type

**Response mapping**:

| Field | Source |
|-------|--------|
| `title` | Title deed number or property identifier |
| `subtitle` | Address (province, district, or full) |
| `status` | null |
| `navigateTo` | `/appraisal/{appraisalId}/property/{propertyId}` |
| `metadata.propertyType` | Type label (Land, Condo, Building, Land+Building) |
| `metadata.address` | Full address string |
| `metadata.titleDeedNumber` | Title deed number |
| `metadata.area` | Area with unit (e.g., `150 sq.wa`) |
| `metadata.linkedAppraisalId` | Related appraisal ID |

---

## Search Behavior

### Matching
- Case-insensitive matching
- Match against the search fields listed per category
- Use `LIKE '%query%'` or equivalent full-text search
- Results ordered by relevance (exact match first, then partial)

### Filtering
- When `filter=all`: search all 3 categories, return up to `limit` results per category
- When `filter={category}`: search only that category, return up to `limit` results
- Empty categories should return empty arrays, not be omitted

### `totalCount`
- Total number of matches across all categories (not limited by `limit`)
- Used by frontend to show "View all" links when `totalCount > limit`

---

## Error Responses

### 400 Bad Request

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Search query must be at least 2 characters.",
  "errors": {
    "q": ["Search query must be at least 2 characters."]
  }
}
```

### 401 Unauthorized
Standard auth error response (existing pattern).

---

## Performance Considerations

- **Debounce**: Frontend debounces at 300ms, but backend should still be efficient since users type fast
- **Query timeout**: Consider a query timeout (e.g., 3s) to prevent long-running searches from blocking
- **Indexing**: Ensure search fields have appropriate database indexes:
  - `requests`: index on request number, customer name
  - `customers`: index on name, customer ID, company name
  - `properties`: index on title deed number, address
- **Result cap**: Even if `totalCount` is large, never return more than `limit` items per category
- **SQL injection**: Use parameterized queries — never concatenate user input into SQL

---

## Example

### Request

```
GET /api/search?q=REQ-2024&filter=all&limit=5
```

### Response

```json
{
  "results": {
    "requests": [
      {
        "id": "abc-123",
        "title": "REQ-2024-001",
        "subtitle": "John Smith",
        "status": "pending",
        "category": "requests",
        "navigateTo": "/requests/abc-123",
        "icon": null,
        "metadata": {
          "requestNumber": "REQ-2024-001",
          "status": "Pending",
          "customerName": "John Smith",
          "collateralType": "Land",
          "appraisalPurpose": "Mortgage",
          "createdDate": "2024-08-15",
          "assignedStaff": "Jane Doe"
        }
      }
    ],
    "customers": [],
    "properties": []
  },
  "totalCount": 1
}
```

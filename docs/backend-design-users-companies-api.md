# Backend Data Design: Users & Companies API

## Overview

The admin assignment page and request page currently use hardcoded mock data for user and company selection. This document specifies the backend data model (database tables, API endpoints, response DTOs) needed to replace the mock data with real API calls.

---

## 1. Database Tables

### 1.1 `users` Table

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `employee_id` | `VARCHAR(20)` | NOT NULL, UNIQUE | e.g. "EMP001", "P000000001" |
| `name` | `VARCHAR(255)` | NOT NULL | Full name |
| `email` | `VARCHAR(255)` | NOT NULL, UNIQUE | |
| `department` | `VARCHAR(255)` | NOT NULL | |
| `avatar_url` | `VARCHAR(500)` | NULL | Image URL or null |
| `is_active` | `BOOLEAN` | NOT NULL, DEFAULT true | Soft deactivation flag |
| `created_on` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |
| `updated_on` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |

**Indexes:**
- `idx_users_employee_id` UNIQUE on `employee_id`
- `idx_users_name_search` on `LOWER(name)` (or GIN trigram for ILIKE partial matching)
- `idx_users_is_active` on `is_active`

**Computed field (NOT stored in table):**
- `current_workload` — computed at query time:

```sql
SELECT u.*, COUNT(a.id) AS current_workload
FROM users u
LEFT JOIN assignments a ON a.assignee_user_id = u.id
  AND a.assignment_status IN ('assigned', 'accepted')
WHERE u.is_active = true
GROUP BY u.id
```

---

### 1.2 `companies` Table

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `company_name` | `VARCHAR(255)` | NOT NULL | |
| `registration_no` | `VARCHAR(50)` | NOT NULL, UNIQUE | e.g. "TAC-001" |
| `contact_person` | `VARCHAR(255)` | NOT NULL | |
| `contact_phone` | `VARCHAR(50)` | NOT NULL | |
| `contact_email` | `VARCHAR(255)` | NOT NULL | |
| `is_active` | `BOOLEAN` | NOT NULL, DEFAULT true | |
| `created_on` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |
| `updated_on` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |

**Indexes:**
- `idx_companies_registration_no` UNIQUE on `registration_no`
- `idx_companies_name_search` on `LOWER(company_name)` (or GIN trigram for ILIKE)
- `idx_companies_is_active` on `is_active`

**Computed fields (NOT stored in table):**

`rating`:
```sql
SELECT c.*, COALESCE(AVG(r.score), 0) AS rating
FROM companies c
LEFT JOIN company_ratings r ON r.company_id = c.id
GROUP BY c.id
```

`active_assignments`:
```sql
SELECT c.*, COUNT(a.id) AS active_assignments
FROM companies c
LEFT JOIN assignments a ON a.assignee_company_id = c.id
  AND a.assignment_status IN ('assigned', 'accepted')
GROUP BY c.id
```

---

### 1.3 `company_ratings` Table (new)

Stores per-assignment ratings for companies, used to compute average `rating`.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `company_id` | `UUID` | FK → companies.id, NOT NULL | |
| `assignment_id` | `UUID` | FK → assignments.id, NULL | Optional link to assignment |
| `score` | `DECIMAL(2,1)` | NOT NULL, CHECK (1.0 <= score <= 5.0) | Rating value |
| `rated_by` | `UUID` | FK → users.id, NOT NULL | User who gave the rating |
| `created_on` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |

**Indexes:**
- `idx_company_ratings_company_id` on `company_id`

---

## 2. API Endpoints

### 2.1 Users

#### `GET /users` — Search/list users (paginated)

**Query Parameters:**

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `q` | string | `""` | Search term — matches against `name` and `employee_id` (case-insensitive) |
| `PageNumber` | int | `0` | Zero-based page index |
| `PageSize` | int | `20` | Number of items per page |
| `IsActive` | bool | `true` | Filter by active/inactive status |

**Response (200 OK):**

```json
{
  "result": {
    "items": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "employeeId": "EMP001",
        "name": "Somchai Prasert",
        "email": "somchai.prasert@lhbank.co.th",
        "department": "Appraisal Department",
        "avatarUrl": null,
        "currentWorkload": 3,
        "isActive": true,
        "createdOn": "2025-01-15T09:00:00+07:00"
      }
    ],
    "count": 42,
    "pageNumber": 0,
    "pageSize": 20
  }
}
```

**Search behavior:**
- When `q` is empty, return all active users (paginated)
- When `q` is provided, match against `name` (ILIKE `%q%`) OR `employee_id` (ILIKE `%q%`)
- Results sorted by `name` ASC by default

---

#### `GET /users/{id}` — Get user by ID

**Path Parameters:**
- `id` (UUID) — User ID

**Response (200 OK):**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "employeeId": "EMP001",
  "name": "Somchai Prasert",
  "email": "somchai.prasert@lhbank.co.th",
  "department": "Appraisal Department",
  "avatarUrl": null,
  "currentWorkload": 3,
  "isActive": true,
  "createdOn": "2025-01-15T09:00:00+07:00",
  "updatedOn": "2025-02-01T14:30:00+07:00"
}
```

**Response (404):**
```json
{
  "message": "User not found"
}
```

---

### 2.2 Companies

#### `GET /companies` — Search/list companies (paginated)

**Query Parameters:**

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `q` | string | `""` | Search term — matches against `company_name` and `registration_no` (case-insensitive) |
| `PageNumber` | int | `0` | Zero-based page index |
| `PageSize` | int | `20` | Number of items per page |
| `IsActive` | bool | `true` | Filter by active/inactive status |

**Response (200 OK):**

```json
{
  "result": {
    "items": [
      {
        "id": "660e8400-e29b-41d4-a716-446655440001",
        "companyName": "Thai Appraisal Co., Ltd.",
        "registrationNo": "TAC-001",
        "contactPerson": "Sompong Chaiyasit",
        "contactPhone": "02-123-4567",
        "contactEmail": "contact@thaiappraisal.co.th",
        "rating": 4.5,
        "activeAssignments": 12,
        "isActive": true,
        "createdOn": "2025-01-10T08:00:00+07:00"
      }
    ],
    "count": 15,
    "pageNumber": 0,
    "pageSize": 20
  }
}
```

**Search behavior:**
- When `q` is empty, return all active companies (paginated)
- When `q` is provided, match against `company_name` (ILIKE `%q%`) OR `registration_no` (ILIKE `%q%`)
- Results sorted by `company_name` ASC by default

---

#### `GET /companies/{id}` — Get company by ID

**Path Parameters:**
- `id` (UUID) — Company ID

**Response (200 OK):**

```json
{
  "id": "660e8400-e29b-41d4-a716-446655440001",
  "companyName": "Thai Appraisal Co., Ltd.",
  "registrationNo": "TAC-001",
  "contactPerson": "Sompong Chaiyasit",
  "contactPhone": "02-123-4567",
  "contactEmail": "contact@thaiappraisal.co.th",
  "rating": 4.5,
  "activeAssignments": 12,
  "isActive": true,
  "createdOn": "2025-01-10T08:00:00+07:00",
  "updatedOn": "2025-02-15T10:00:00+07:00"
}
```

**Response (404):**
```json
{
  "message": "Company not found"
}
```

---

## 3. Response DTO Summary

### UserDto (list item)

| Field | Type | Description |
|-------|------|-------------|
| `id` | string (UUID) | Primary key |
| `employeeId` | string | Employee code (e.g. "EMP001") |
| `name` | string | Full name |
| `email` | string | Email address |
| `department` | string | Department name |
| `avatarUrl` | string \| null | Avatar image URL |
| `currentWorkload` | int | **Computed:** count of active assignments for this user |
| `isActive` | boolean | Whether user is active |
| `createdOn` | datetime | Creation timestamp |

### UserDetailDto (single item — adds `updatedOn`)

All fields from UserDto, plus:

| Field | Type | Description |
|-------|------|-------------|
| `updatedOn` | datetime | Last update timestamp |

---

### CompanyDto (list item)

| Field | Type | Description |
|-------|------|-------------|
| `id` | string (UUID) | Primary key |
| `companyName` | string | Company name |
| `registrationNo` | string | Registration number (e.g. "TAC-001") |
| `contactPerson` | string | Primary contact name |
| `contactPhone` | string | Contact phone number |
| `contactEmail` | string | Contact email |
| `rating` | number | **Computed:** average rating from company_ratings (1.0-5.0) |
| `activeAssignments` | int | **Computed:** count of active assignments for this company |
| `isActive` | boolean | Whether company is active |
| `createdOn` | datetime | Creation timestamp |

### CompanyDetailDto (single item — adds `updatedOn`)

All fields from CompanyDto, plus:

| Field | Type | Description |
|-------|------|-------------|
| `updatedOn` | datetime | Last update timestamp |

---

## 4. Paginated Response Wrapper

All list endpoints use the same pagination wrapper (consistent with existing `/requests` endpoint):

```json
{
  "result": {
    "items": [ ...DTOs... ],
    "count": 42,
    "pageNumber": 0,
    "pageSize": 20
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Array of DTOs for current page |
| `count` | int | Total number of matching records |
| `pageNumber` | int | Current page (zero-based) |
| `pageSize` | int | Page size |

---

## 5. Computed Fields Reference

These fields are NOT stored in the database. They must be computed at query time via JOIN/subquery.

| Field | Entity | SQL Logic |
|-------|--------|-----------|
| `currentWorkload` | User | `COUNT(assignments) WHERE assignee_user_id = user.id AND assignment_status IN ('assigned', 'accepted')` |
| `rating` | Company | `COALESCE(AVG(company_ratings.score), 0) WHERE company_id = company.id` |
| `activeAssignments` | Company | `COUNT(assignments) WHERE assignee_company_id = company.id AND assignment_status IN ('assigned', 'accepted')` |

The `assignments` table already exists — it has `assignee_user_id` and `assignee_company_id` columns.

---

## 6. Frontend Field Mapping (for reference)

When the frontend integrates with these APIs, the following field name changes apply:

### Admin page: `InternalStaff` type → `UserDto`

| Current Frontend Field | New API Field | Change |
|----------------------|---------------|--------|
| `avatar` | `avatarUrl` | Renamed |
| All other fields | Same names | No change |

### Request page: `SearchUserModal` mock → `UserDto`

| Current Frontend Field | New API Field | Change |
|----------------------|---------------|--------|
| `userId` | `employeeId` | Renamed |
| `username` | `name` | Renamed |

---

## 7. Notes

- **Unified User API:** Both admin assignment (staff selection) and request (requestor selection) pages will use the same `/users` endpoint. No need for separate `/staff` endpoint.
- **Soft delete:** Both tables use `is_active` flag instead of hard delete.
- **Rating system:** The `company_ratings` table supports per-assignment ratings with history. If rating is not needed initially, the `rating` field can default to `0` and the `company_ratings` table can be created later.
- **Search performance:** For production with many records, consider PostgreSQL GIN trigram indexes (`pg_trgm` extension) for efficient partial name matching.

# Address API Contract — Two Sources (Title & DOPA)

## Overview

The frontend needs **two separate address datasets** for the address autocomplete component. Each dataset comes from a different authority and is used for different address fields in the application.

| Source | Description | Endpoint |
|--------|-------------|----------|
| **Title** | Land title records (กรมที่ดิน) — larger dataset | `GET /parameters/addresses/title` |
| **DOPA** | Dept of Provincial Administration (กรมการปกครอง) — smaller dataset | `GET /parameters/addresses/dopa` |

### Why two datasets?

- **Title addresses** come from the Department of Lands (กรมที่ดิน). They include historical/merged sub-districts that still appear on land title documents. Used for title address fields (`titleAddress.*`) and detail address fields.
- **DOPA addresses** come from the Department of Provincial Administration (กรมการปกครอง). They reflect current official administrative divisions only. Used for DOPA address fields (`dopaAddress.*`).

## Endpoints

### 1. Title Addresses

```
GET /parameters/addresses/title
```

### 2. DOPA Addresses

```
GET /parameters/addresses/dopa
```

### Authentication
- Bearer token (same as other endpoints)

### Response (identical shape for both)

**Status:** `200 OK`
**Content-Type:** `application/json`
**Body:** Flat array of address objects — one entry per sub-district (denormalized)

```json
[
  {
    "provinceCode": "10",
    "provinceName": "กรุงเทพมหานคร",
    "districtCode": "1001",
    "districtName": "พระนคร",
    "subDistrictCode": "100101",
    "subDistrictName": "พระบรมมหาราชวัง",
    "postcode": "10200"
  },
  {
    "provinceCode": "10",
    "provinceName": "กรุงเทพมหานคร",
    "districtCode": "1001",
    "districtName": "พระนคร",
    "subDistrictCode": "100102",
    "subDistrictName": "วังบูรพาภิรมย์",
    "postcode": "10200"
  },
  {
    "provinceCode": "50",
    "provinceName": "เชียงใหม่",
    "districtCode": "5001",
    "districtName": "เมืองเชียงใหม่",
    "subDistrictCode": "500101",
    "subDistrictName": "ศรีภูมิ",
    "postcode": "50200"
  }
]
```

## Field Specification

| Field | Type | Format | Description | Example |
|-------|------|--------|-------------|---------|
| `provinceCode` | `string` | 2 digits | Province code | `"10"` |
| `provinceName` | `string` | Thai text | Province name in Thai | `"กรุงเทพมหานคร"` |
| `districtCode` | `string` | 4 digits | District code (province prefix + 2 digits) | `"1001"` |
| `districtName` | `string` | Thai text | District name in Thai | `"พระนคร"` |
| `subDistrictCode` | `string` | 6 digits | Sub-district code (district prefix + 2 digits) | `"100101"` |
| `subDistrictName` | `string` | Thai text | Sub-district name in Thai | `"พระบรมมหาราชวัง"` |
| `postcode` | `string` | 5 digits | Postal/zip code | `"10200"` |

## Dataset Comparison

| | Title (`/parameters/addresses/title`) | DOPA (`/parameters/addresses/dopa`) |
|---|---|---|
| Source authority | กรมที่ดิน (Department of Lands) | กรมการปกครอง (DOPA) |
| Size | More entries (includes historical/merged sub-districts) | Fewer entries (current administrative divisions only) |
| Used for | Title address, detail address, appraisal land/condo/building address | DOPA address fields |
| Update frequency | Less frequent | Follows official administrative changes |

## Frontend Usage

| Form field | Endpoint used |
|------------|---------------|
| Request → Detail Address (`detail.address.*`) | `title` |
| Request → Title Address (`titleAddress.*`) | `title` |
| Request → DOPA Address (`dopaAddress.*`) | `dopa` |
| Appraisal → Land Info address | `title` |
| Appraisal → Condo/Building address | `title` |

## Important Notes

1. **Naming convention:** camelCase (matches frontend TypeScript interface)
2. **One row per sub-district:** Each entry is fully denormalized — province and district info is repeated for every sub-district under them
3. **All active records:** Return all active sub-districts for that source
4. **Code hierarchy:** `subDistrictCode` starts with its parent `districtCode`, which starts with its parent `provinceCode`
5. **No pagination:** Frontend fetches all records at once and caches for the entire session
6. **Both endpoints called on app load** — frontend calls both in parallel at startup
7. **Fallback:** If either endpoint fails or returns empty, frontend falls back to mock data

## SQL Reference (if using separate tables per source)

```sql
-- Title addresses
SELECT
    p.Code        AS provinceCode,
    p.Name        AS provinceName,
    d.Code        AS districtCode,
    d.Name        AS districtName,
    sd.Code       AS subDistrictCode,
    sd.Name       AS subDistrictName,
    sd.Postcode   AS postcode
FROM TitleSubDistricts sd
JOIN TitleDistricts d ON sd.DistrictCode = d.Code
JOIN TitleProvinces p ON d.ProvinceCode = p.Code
WHERE sd.IsActive = 1
ORDER BY sd.Code

-- DOPA addresses
SELECT
    p.Code        AS provinceCode,
    p.Name        AS provinceName,
    d.Code        AS districtCode,
    d.Name        AS districtName,
    sd.Code       AS subDistrictCode,
    sd.Name       AS subDistrictName,
    sd.Postcode   AS postcode
FROM DopaSubDistricts sd
JOIN DopaDistricts d ON sd.DistrictCode = d.Code
JOIN DopaProvinces p ON d.ProvinceCode = p.Code
WHERE sd.IsActive = 1
ORDER BY sd.Code
```

If both sources share the same table with a `Source` column:

```sql
SELECT
    p.Code        AS provinceCode,
    p.Name        AS provinceName,
    d.Code        AS districtCode,
    d.Name        AS districtName,
    sd.Code       AS subDistrictCode,
    sd.Name       AS subDistrictName,
    sd.Postcode   AS postcode
FROM SubDistricts sd
JOIN Districts d ON sd.DistrictCode = d.Code
JOIN Provinces p ON d.ProvinceCode = p.Code
WHERE sd.IsActive = 1
  AND sd.Source = @source  -- 'title' or 'dopa'
ORDER BY sd.Code
```

## Caching Behavior

- Frontend caches both responses for the **entire browser session** (no re-fetching)
- Consider adding `Cache-Control` headers for HTTP-level caching if desired
- Address data changes infrequently

## Error Responses

| Status | Description |
|--------|-------------|
| `401` | Unauthorized — missing or invalid token |
| `500` | Internal server error |

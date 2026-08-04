# Address API Contract — Two Sources (Title & DOPA)

## Overview

The frontend needs **two separate address datasets** for the address autocomplete component. Each dataset comes from a different authority and is used for different address fields in the application.

| Source | Description | Endpoint |
|--------|-------------|----------|
| **Title** | Land title records (กรมที่ดิน) — larger dataset | `GET /parameters/addresses/title` |
| **DOPA** | Dept of Provincial Administration (กรมการปกครอง) — smaller dataset | `GET /parameters/addresses/dopa` |

### Why two datasets?

- **Title addresses** come from the Department of Lands (กรมที่ดิน). They include historical/merged sub-districts that still appear on land title documents. Used for title address fields (`titleAddress.*`) and detail address fields. Seeded from `.claude/docs/MasterAddressCheckUseV2.xlsx` (rows the business flagged `UserDelete = 1` are excluded).
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
    "provinceNameEn": "Bangkok",
    "districtCode": "1001",
    "districtName": "พระนคร",
    "districtNameEn": "Phra Nakhon",
    "subDistrictCode": "100101",
    "subDistrictName": "พระบรมมหาราชวัง",
    "subDistrictNameEn": "Phra Borom Maha Ratchawang",
    "postcode": "10200"
  },
  {
    "provinceCode": "10",
    "provinceName": "กรุงเทพมหานคร",
    "provinceNameEn": "Bangkok",
    "districtCode": "10A6",
    "districtName": "สวนหลวง(พระโขนง)",
    "districtNameEn": "สวนหลวง(พระโขนง)",
    "subDistrictCode": "383798",
    "subDistrictName": "สวนหลวง,คลองตัน(ที่8พระโขนงฝั่งเหนือ)",
    "subDistrictNameEn": "สวนหลวง,คลองตัน(ที่8พระโขนงฝั่งเหนือ)",
    "postcode": null
  }
]
```

## Field Specification

| Field | Type | Format | Description | Example |
|-------|------|--------|-------------|---------|
| `provinceCode` | `string` | 2 chars | Province code | `"10"` |
| `provinceName` | `string` | Thai text | Province name in Thai | `"กรุงเทพมหานคร"` |
| `provinceNameEn` | `string` | Latin text | Province name in English — **falls back to the Thai name** when no English name is known | `"Bangkok"` |
| `districtCode` | `string` | 4 chars | District code | `"1001"` |
| `districtName` | `string` | Thai text | District name in Thai | `"พระนคร"` |
| `districtNameEn` | `string` | Latin text | District name in English — falls back to the Thai name | `"Phra Nakhon"` |
| `subDistrictCode` | `string` | 6 chars | Sub-district code | `"100101"` |
| `subDistrictName` | `string` | Thai text | Sub-district name in Thai | `"พระบรมมหาราชวัง"` |
| `subDistrictNameEn` | `string` | Latin text | Sub-district name in English — falls back to the Thai name | `"Phra Borom Maha Ratchawang"` |
| `postcode` | `string \| null` | 5 digits | Postal/zip code. **Nullable** — `null` for the 3,715 Title sub-districts the master carries no postcode for. Always populated for DOPA. | `"10200"` |

## Dataset Comparison

| | Title (`/parameters/addresses/title`) | DOPA (`/parameters/addresses/dopa`) |
|---|---|---|
| Source authority | กรมที่ดิน (Department of Lands) | กรมการปกครอง (DOPA) |
| Size | 93 provinces / 1,640 districts / **11,144 sub-districts** | 77 / 928 / **7,436 sub-districts** |
| Code format | Not always numeric — provinces `A0`/`A1`/`A2`, districts like `10G8`, `10A6` | Numeric TIS-1099 codes only |
| `postcode` | Nullable (3,715 rows are `null`) | Always present |
| Used for | Title (deed) address, appraisal land/condo/building address | Detail (Location) address, DOPA address fields |
| Update frequency | Less frequent | Follows official administrative changes |

## Frontend Usage

| Form field | Endpoint used |
|------------|---------------|
| Request → Detail Address / Location (`detail.address.*`) | `dopa` |
| Request → Title Address (`titleAddress.*`) | `title` |
| Request → DOPA Address (`dopaAddress.*`) | `dopa` |
| Appraisal → Land Info address | `title` |
| Appraisal → Condo/Building address | `title` |

## Important Notes

1. **Naming convention:** camelCase (matches frontend TypeScript interface)
2. **One row per sub-district:** Each entry is fully denormalized — province and district info is repeated for every sub-district under them
3. **All records:** Both masters return every seeded sub-district. There is no `IsActive` flag on these tables — rows the business flagged for deletion are already excluded at seed time.
4. **Code hierarchy — DOPA only.** For DOPA, `subDistrictCode` starts with its parent `districtCode`, which starts with its parent `provinceCode`.
   **This does NOT hold for Title.** In the Title master 118 sub-districts sit under a district that is not their code prefix, 206 district codes and 161 sub-district codes are not purely numeric, and three province codes are `A0`/`A1`/`A2`. Always resolve a Title parent by joining `DistrictCode` / `ProvinceCode` — never by slicing the code.
5. **No pagination:** Frontend fetches all records at once and caches for the entire session
6. **Both endpoints called on app load** — frontend calls both in parallel at startup
7. **Fallback:** If either endpoint fails or returns empty, frontend falls back to mock data
8. **The two datasets are different.** Title is the Land Department master (11,144 sub-districts,
   incl. historical/merged ones that still appear on deeds); DOPA is the current administrative list
   (7,436). 3,715 Title codes exist in **neither** DOPA table, so a geocode captured through the
   Title picker will not resolve against `parameter.Dopa*` — this affects the DOPA-sourced fields in
   `vw_RegulatoryExport` and `GetAppraisalResultQueryHandler`. Always resolve a stored geocode
   against the master the capturing form used.
9. **`postcode` can be `null` on the Title endpoint.** The Title master carries no postcode column;
   postcodes are only present for the 7,429 codes that overlap the DOPA list. Do not assume a
   postcode is available when auto-filling from the Title picker.

## SQL Reference

```sql
-- Title addresses (what AddressRepository.GetTitleAddressesAsync produces)
SELECT
    p.Code        AS provinceCode,
    p.NameTh      AS provinceName,
    p.NameEn      AS provinceNameEn,
    d.Code        AS districtCode,
    d.NameTh      AS districtName,
    d.NameEn      AS districtNameEn,
    sd.Code       AS subDistrictCode,
    sd.NameTh     AS subDistrictName,
    sd.NameEn     AS subDistrictNameEn,
    sd.Postcode   AS postcode          -- NULL for 3,715 rows
FROM parameter.TitleSubDistricts sd
JOIN parameter.TitleDistricts d ON sd.DistrictCode = d.Code
JOIN parameter.TitleProvinces p ON d.ProvinceCode = p.Code
ORDER BY sd.Code;

-- DOPA addresses: identical, against parameter.Dopa*
```

## Caching Behavior

### Frontend
- Caches both responses for the **entire browser session** (no re-fetching). A user will not see a
  master-data change until they reload the page.

### Backend
- `CachedAddressRepository` caches both responses in-process (`IMemoryCache`) with a **5-minute
  absolute TTL**, keyed `addresses:title` / `addresses:dopa`.
- **The cache is per app node and is never invalidated.** Production runs two IIS nodes, so each
  holds and expires its own copy independently — there is no cross-node invalidation, and a
  `cache.Remove` would only affect the node that served the request.
- Consequence: after the masters are changed — via the admin CRUD under
  `/parameters/addresses/{dataset}/…` (`Addresses/Features/AdminAddresses/AddressAdminEndpoints.cs`)
  or via the DbUp seed scripts — the change takes **up to 5 minutes to appear**, on the node that
  served the write as well as the other one. It then still requires a browser reload to reach the
  user.
- ⚠ The admin CRUD endpoints do **not** invalidate this cache. Adding a `cache.Remove` there would
  make the change immediate on the writing node only, so the TTL remains the bound for the rest.
- To publish a change immediately, recycle both app pools — that clears the in-process cache at
  once rather than waiting out the TTL.
- No HTTP-level caching is applied: these endpoints emit no `Cache-Control`, `ETag`, or
  `Last-Modified`, and response compression is not enabled, so every client request re-serializes
  and re-transfers the full array.

## Error Responses

| Status | Description |
|--------|-------------|
| `401` | Unauthorized — missing or invalid token |
| `500` | Internal server error |

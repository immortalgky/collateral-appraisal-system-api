# Design: Multi-Language MarketComparableFactor

## Understanding Summary
1. **What**: Add multi-language support for `MarketComparableFactor.FactorName`
2. **Why**: Factor descriptions need to be displayed in different languages
3. **Who**: Appraisers/admins using the system in different locales
4. **How**: Separate `MarketComparableFactorTranslations` owned entity table; `FactorName` removed from main entity
5. **Constraints**: English translation always required; fallback to English when requested language missing
6. **API**: List endpoint supports `?lang=th` filter; always returns translations array. Create/Update accept translations inline.
7. **Non-goal**: Does NOT extend to other entities — only `MarketComparableFactor.FactorName`

## Assumptions
- Existing `FactorName` values migrated as `en` translations
- Language codes are ISO 639-1 strings (e.g., `"en"`, `"th"`)
- No language management table — languages are just string codes
- Deleting a factor cascade-deletes its translations (owned entity behavior)
- 2-5 languages expected; negligible performance impact

## Data Model

### MarketComparableFactors (updated)
| Column | Type | Notes |
|---|---|---|
| Id | uniqueidentifier | PK |
| FactorCode | nvarchar(50) | Unique index |
| ~~FactorName~~ | ~~nvarchar(200)~~ | **REMOVED** |
| FieldName | nvarchar(100) | |
| DataType | nvarchar(20) | |
| FieldLength | int? | |
| FieldDecimal | int? | |
| ParameterGroup | nvarchar(100) | |
| IsActive | bit | |

### MarketComparableFactorTranslations (new)
| Column | Type | Notes |
|---|---|---|
| MarketComparableFactorId | uniqueidentifier | Composite PK, FK to MarketComparableFactors |
| Language | nvarchar(10) | Composite PK, ISO 639-1 |
| FactorName | nvarchar(200) | Required |

## API Contract

### Create Factor
```json
POST /market-comparable-factors
{
  "factorCode": "LOCATION",
  "fieldName": "location",
  "dataType": "Text",
  "translations": [
    { "language": "en", "factorName": "Location" },
    { "language": "th", "factorName": "ทำเล" }
  ]
}
```

### Update Factor
```json
PUT /market-comparable-factors/{id}
{
  "fieldName": "location",
  "dataType": "Text",
  "translations": [
    { "language": "en", "factorName": "Location" },
    { "language": "th", "factorName": "ทำเล" }
  ]
}
```

### List Factors
```
GET /market-comparable-factors                  -> all translations
GET /market-comparable-factors?lang=th          -> filtered to Thai (English fallback)
GET /market-comparable-factors?activeOnly=true  -> existing filter still works
```

Response (consistent shape):
```json
{
  "factors": [
    {
      "id": "...",
      "factorCode": "LOCATION",
      "fieldName": "location",
      "dataType": "Text",
      "fieldLength": null,
      "fieldDecimal": null,
      "parameterGroup": null,
      "isActive": true,
      "translations": [
        { "language": "en", "factorName": "Location" },
        { "language": "th", "factorName": "ทำเล" }
      ]
    }
  ]
}
```

With `?lang=zh` (no Chinese, falls back to English):
```json
{
  "translations": [
    { "language": "en", "factorName": "Location" }
  ]
}
```

## Decision Log

| # | Decision | Alternatives Considered | Rationale |
|---|---|---|---|
| 1 | Separate translation table (no fallback on main entity) | Same-table rows per language, JSON column | Clean separation; FactorId FK unchanged for downstream |
| 2 | Owned entity collection | Separate entity with own repo, JSON column | DDD-aligned, least boilerplate, managed inline |
| 3 | English always required | Any language, no enforcement | Reliable fallback |
| 4 | Fallback to English | Return null, return error | Best UX |
| 5 | Single consistent response shape (translations array) | Dual shape | Simpler frontend contract |
| 6 | Inline translations on Create/Update | Separate endpoints | Factors and names managed together |
| 7 | ISO 639-1 string codes | Enum, language table | Open-ended extensibility |

## Edge Cases
- Create without English -> domain rejects
- Duplicate language in request -> domain rejects
- Delete factor -> cascade deletes translations
- `?lang=xx` no match -> fallback to English

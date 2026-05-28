# Task: GET /appraisals/{appraisalId}/map-pins

## Context
New endpoint for 360-summary map feature. Returns the appraisal's own collateral locations (land + condo properties with coords) and its own linked market comparables with coords. Current appraisal is in-progress, not returned by history-search (which requires CompletedAt IS NOT NULL).

## Auth policy
Reuse `"history-search.view"` — same policy used by HistorySearchEndpoint and GetCollateralEngagementDetailEndpoint.

## Files to create
- [x] `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalMapPins/GetAppraisalMapPinsEndpoint.cs`
- [x] `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalMapPins/GetAppraisalMapPinsQuery.cs`
- [x] `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalMapPins/GetAppraisalMapPinsResult.cs`
- [x] `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalMapPins/GetAppraisalMapPinsQueryHandler.cs`
- [x] `Tests/Integration/Collateral.Integration.Tests/GetAppraisalMapPinsEndpointTests.cs`

## SQL queries
### Collateral pins (one per property with non-null coords)
UNION land + condo appraisal details, filtered to AppraisalId, where lat/lon not null.

### Comparable pins
JOIN AppraisalComparables ac to MarketComparables mc where ac.AppraisalId = @AppraisalId AND mc.IsDeleted = 0 AND mc.Latitude IS NOT NULL AND mc.Longitude IS NOT NULL.

## Review
- Reused `history-search.view` auth policy (consistent with sibling endpoints)
- Sequential Dapper queries on GetOpenConnection() (simplest safe pattern; no MARS issue since sequential not parallel)
- DTOs match contract exactly

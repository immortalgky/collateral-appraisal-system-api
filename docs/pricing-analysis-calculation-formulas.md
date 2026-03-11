# Pricing Analysis — Calculation Formulas

Technical reference for backend implementation of the three pricing analysis calculation methods.

> **Audience:** Backend engineers
> **Source of truth:** Frontend implementation in `src/features/pricingAnalysis/domain/` and `src/features/pricingAnalysis/adapters/`

---

## Table of Contents

1. [Common Utilities](#1-common-utilities)
2. [Shared Formulas (All Methods)](#2-shared-formulas-all-methods)
3. [Method 1: Direct Comparison (DC)](#3-method-1-direct-comparison-dc)
4. [Method 2: Sale Adjustment Grid (SAG)](#4-method-2-sale-adjustment-grid-sag)
5. [Method 3: Weighted Quality Scores (WQS)](#5-method-3-weighted-quality-scores-wqs)
6. [Summary Comparison Table](#6-summary-comparison-table)

---

## 1. Common Utilities

### `round2(n)`
Round to 2 decimal places.

```
round2(n) = Math.round(n × 100) / 100
```

If `n` is not a finite number, return `0`.

### `floorToTenThousands(n)`
Floor to the nearest 10,000.

```
floorToTenThousands(n) = Math.floor(n / 10000) × 10000
```

### `calcSum(values[])`
Sum an array of numbers, round to 2 decimals.

```
calcSum(values) = round2(Σ values[i])
```

---

## 2. Shared Formulas (All Methods)

These formulas are used identically across DC, SAG, and WQS.

### 2.1 Adjusted Value (First Revision)

The adjusted value depends on whether the market comparable has an **offering price** or a **selling price**.

#### Case A: From Offering Price

Priority logic:
1. If `offeringPriceAdjustmentPct > 0`:
   ```
   adjustedValue = round2(offeringPrice − offeringPrice × offeringPriceAdjustmentPct / 100)
   ```
2. Else if `offeringPriceAdjustmentAmt > 0`:
   ```
   adjustedValue = round2(offeringPriceAdjustmentAmt)
   ```
   > **Note:** When an adjustment amount is provided, the result is the amount itself (not `offeringPrice − amt`).
3. Else (no adjustment):
   ```
   adjustedValue = round2(offeringPrice)
   ```

| Input Field | Description |
|---|---|
| `offeringPrice` | Market comparable's offer price (from survey data) |
| `offeringPriceAdjustmentPct` | User-entered adjustment percentage |
| `offeringPriceAdjustmentAmt` | User-entered adjustment amount |

#### Case B: From Selling Price (Time Adjustment)

```
adjustedValue = round2(sellingPrice + sellingPrice × numberOfYears × sellingPriceAdjustmentYearPct / 100)
```

| Input Field | Description |
|---|---|
| `sellingPrice` | Market comparable's sale price (from survey data) |
| `numberOfYears` | Number of years since the sale |
| `sellingPriceAdjustmentYearPct` | Annual price adjustment percentage |

#### Total Adjusted Selling Price (display field)

```
totalAdjustedSellingPrice = round2(numberOfYears × sellingPriceAdjustmentYearPct)
```

> This is a display-only intermediate showing the total percentage adjustment applied.

### 2.2 Selection Logic

- If the survey has `offerPrice` → use Case A (offering price adjustment)
- If the survey has `salePrice` (and no `offerPrice`) → use Case B (selling price adjustment)
- If neither → `adjustedValue = 0`

---

## 3. Method 1: Direct Comparison (DC)

### Overview

Compares the subject property directly against multiple market comparables. Calculates adjustments for area differences and qualitative factors. The **final value is the minimum** across all comparable total adjusted values.

### 3.1 Second Revision (Area Adjustments)

#### Land Area Difference

```
landAreaDiff = round2(propertyLandArea − surveyLandArea)
```

- `propertyLandArea`: Subject property's land area (factor code `05`)
- `surveyLandArea`: Market comparable's land area (factor code `05`)

#### Land Value Increase/Decrease

```
landValueIncreaseDecrease = round2(landPrice × landAreaDiff)
```

- `landPrice`: User-entered unit price per land area unit

#### Usable Area Difference

```
usableAreaDiff = round2(propertyUsableArea − surveyUsableArea)
```

- `propertyUsableArea`: Subject property's usable area (factor code `12`)
- `surveyUsableArea`: Market comparable's usable area (factor code `12`)

#### Building Value Increase/Decrease

```
buildingValueIncreaseDecrease = round2(usableAreaPrice × usableAreaDiff)
```

- `usableAreaPrice`: User-entered unit price per usable area unit

#### Total Second Revision

```
totalSecondRevision = round2(adjustedValue + buildingValueIncreaseDecrease + landValueIncreaseDecrease)
```

### 3.2 Adjustment Factors (Qualitative)

Each factor row has a qualitative level per survey column.

#### Qualitative Level Default Percent

| Level | Default Percent |
|---|---|
| `E` (Equal) | 0% |
| `I` (Inferior) | +5% |
| `B` (Better) | -5% |
| empty/null | 0% |

> The user can override this default percentage.

#### Factor Adjustment Amount

```
adjustAmount = round2(totalSecondRevision × adjustPercent / 100)
```

Per factor row and survey column. `totalSecondRevision` is the column's total second revision value.

#### Sum of Factor Percentages

```
sumFactorPct = round2(Σ adjustmentFactors[i].adjustPercent)  // for each survey column
```

#### Sum of Factor Amounts

```
sumFactorAmt = round2(Σ adjustmentFactors[i].adjustAmount)  // for each survey column
```

### 3.3 Total Adjust Value (per survey)

```
totalAdjustValue = round2(totalSecondRevision + sumFactorAmt)
```

### 3.4 Final Value

```
finalValue = MIN(totalAdjustValue[0], totalAdjustValue[1], ..., totalAdjustValue[N-1])
```

The final value is the **minimum** of all survey columns' `totalAdjustValue`. Only finite numbers are considered.

### 3.5 Final Value Rounded

```
finalValueRounded = floorToTenThousands(finalValue)
```

> Auto-calculated but user-editable. Once the user manually edits this field, the auto-calculation stops.

---

## 4. Method 2: Sale Adjustment Grid (SAG)

### Overview

Similar to Direct Comparison but adds **weighting** per survey column. The final value is a **weighted sum** instead of a minimum.

### 4.1 Second Revision (Area Adjustments)

Identical to DC — see [Section 3.1](#31-second-revision-area-adjustments).

### 4.2 Adjustment Factors (Qualitative)

Identical to DC — see [Section 3.2](#32-adjustment-factors-qualitative).

### 4.3 Total Adjust Value (per survey)

Identical to DC — see [Section 3.3](#33-total-adjust-value-per-survey).

### 4.4 Weight (per survey)

```
weight = 1 / numberOfSurveys
```

- Default is equal weighting across all surveys
- Value must be between 0 and 1
- User-editable; auto-default only applies if not yet dirty

### 4.5 Weighted Adjust Value (per survey)

```
weightedAdjustValue = round2(totalAdjustValue × weight)
```

> **Note:** `weight` is a decimal (e.g., 0.33), NOT a percentage.

### 4.6 Final Value

```
finalValue = round2(Σ weightedAdjustValue[i])  // sum across all survey columns
```

### 4.7 Final Value Rounded

```
finalValueRounded = floorToTenThousands(finalValue)
```

> Auto-calculated but user-editable. Once the user manually edits this field, the auto-calculation stops.

---

## 5. Method 3: Weighted Quality Scores (WQS)

### Overview

Uses linear regression to predict the property value based on weighted quality scores. Each scoring factor has a weight, intensity/score for the collateral and each survey, producing weighted scores that feed into a regression model.

### 5.1 Scoring Table

Each row represents a scoring factor. Each row has:

| Field | Description |
|---|---|
| `weight` | Importance weight of the factor |
| `intensity` | Intensity rating for this factor (property context) |
| `weightedIntensity` | Calculated: `weight × intensity` |
| `collateralScore` | Score assigned to the subject property |
| `collateralWeightedScore` | Calculated: `weight × collateralScore` |
| `surveyScore[i]` | Score assigned to survey `i` |
| `weightedSurveyScore[i]` | Calculated: `weight × surveyScore[i]` |

#### Weighted Score Formula

```
weightedScore = round2(weight × score)
```

Used for `weightedIntensity`, `collateralWeightedScore`, and each `weightedSurveyScore`.

### 5.2 Totals Row

Column-wise sums across all scoring factor rows:

```
totalWeight             = calcSum(all weight values)
totalIntensity          = calcSum(all intensity values)
totalWeightedIntensity  = calcSum(all weightedIntensity values)
totalSurveyScore[i]     = calcSum(all surveyScore[i] values)        // per survey column
totalWeightedSurveyScore[i] = calcSum(all weightedSurveyScore[i] values) // per survey column
totalCollateralScore    = calcSum(all collateralScore values)
totalWeightedCollateralScore = calcSum(all collateralWeightedScore values)
```

### 5.3 Calculation Section (Adjusted Values)

Same as shared formulas — see [Section 2.1](#21-adjusted-value-first-revision).

Per survey column, calculate `adjustedValue` from either offering price or selling price.

### 5.4 Regression Analysis

The regression uses:
- **X values (known_xs):** `totalWeightedSurveyScore[]` — one per survey
- **Y values (known_ys):** `adjustedValue[]` — one per survey (from calculation section)
- **Target X:** `totalWeightedCollateralScore` — the subject property's total weighted score

#### FORECAST (Final Value)

Implements Excel's `FORECAST.LINEAR` function:

```
meanX = average(known_xs)
meanY = average(known_ys)

slope = Σ((known_xs[i] − meanX) × (known_ys[i] − meanY)) / Σ((known_xs[i] − meanX)²)
intercept = meanY − slope × meanX

finalValue = round2(intercept + slope × collateralWeightedScore)
```

#### RSQ (Coefficient of Determination / R²)

```
sxx = n × Σ(x²) − (Σx)²
syy = n × Σ(y²) − (Σy)²
sxy = n × Σ(xy) − (Σx)(Σy)

RSQ = (sxy)² / (sxx × syy)
```

Rounded to 4 decimal places. Returns `0` if fewer than 2 data points or zero variance.

#### SLOPE

```
SLOPE = (n × Σ(xy) − Σx × Σy) / (n × Σ(x²) − (Σx)²)
```

Returns `0` if fewer than 2 data points or denominator is zero.

#### INTERCEPT

```
INTERCEPT = meanY − SLOPE × meanX
```

Returns `0` if fewer than 2 data points.

#### STEYX (Standard Error)

```
SSE = Σ(y[i] − (intercept + slope × x[i]))²
STEYX = √(SSE / (n − 2))
```

Rounded to 6 decimal places. Requires at least 3 data points; returns `0` otherwise.

### 5.5 Estimate Range

```
lowestEstimate  = round2(finalValueRounded − standardError)
highestEstimate = round2(finalValueRounded + standardError)
```

### 5.6 Appraisal Price

```
if landArea is provided:
    appraisalPrice = round2(finalValueRounded × landArea)
else if usableArea is provided:
    appraisalPrice = round2(finalValueRounded × usableArea)
else:
    appraisalPrice = round2(finalValueRounded)
```

### 5.7 Final Value Rounded

```
finalValueRounded = floorToTenThousands(finalValue)
```

### 5.8 Appraisal Price Rounded

```
appraisalPriceRounded = floorToTenThousands(appraisalPrice)
```

> Both rounded fields are auto-calculated but user-editable. Once the user manually edits, auto-calculation stops.

---

## 6. Summary Comparison Table

| Aspect | Direct Comparison (DC) | Sale Adjustment Grid (SAG) | Weighted Quality Scores (WQS) |
|---|---|---|---|
| **Adjusted Value** | From offer/sale price | From offer/sale price | From offer/sale price |
| **Area Adjustments** | Land + Building area diffs | Land + Building area diffs | None |
| **Second Revision** | adjustedValue + area adjustments | adjustedValue + area adjustments | N/A |
| **Qualitative Factors** | Per-factor % → amount via totalSecondRevision | Per-factor % → amount via totalSecondRevision | Scoring table with weights |
| **Weighting** | None (uses minimum) | Weight per survey (default: 1/N) | Via regression model |
| **Final Value** | MIN of all totalAdjustValues | SUM of weighted totalAdjustValues | FORECAST.LINEAR regression |
| **Rounding** | floorToTenThousands | floorToTenThousands | floorToTenThousands |
| **Additional Stats** | — | — | R², STEYX, SLOPE, INTERCEPT, estimate range |
| **Appraisal Price** | = finalValueRounded | = finalValueRounded | finalValueRounded × area |

### Calculation Flow Diagrams

#### DC & SAG Flow

```
offerPrice/salePrice
    → adjustedValue (first revision)
    → + landValueIncDec + buildingValueIncDec
    → totalSecondRevision
    → + sumFactorAmt (from qualitative factors)
    → totalAdjustValue
    → DC: MIN(all columns)  /  SAG: Σ(totalAdjustValue × weight)
    → finalValue
    → floorToTenThousands → finalValueRounded
```

#### WQS Flow

```
Scoring Table:
    weight × score → weightedScore (per factor per survey/collateral)
    Σ weightedScore → totalWeightedSurveyScore[i], totalWeightedCollateralScore

Calculation:
    offerPrice/salePrice → adjustedValue[i] (per survey)

Regression:
    FORECAST(totalWeightedCollateralScore, adjustedValues[], totalWeightedSurveyScores[])
    → finalValue
    → floorToTenThousands → finalValueRounded
    → × area → appraisalPrice → floorToTenThousands → appraisalPriceRounded

Stats:
    RSQ, STEYX, SLOPE, INTERCEPT
    lowestEstimate = finalValueRounded − STEYX
    highestEstimate = finalValueRounded + STEYX
```

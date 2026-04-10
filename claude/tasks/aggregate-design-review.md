# Appraisal Module - Aggregate Design Review

## Executive Summary

**Your concern is valid.** The Appraisal aggregate is large, though not unreasonably so for the domain. The main issue is the cross-aggregate relationship between `PricingAnalysis` (separate aggregate) and `PropertyGroup` (owned by Appraisal).

---

## Current Aggregate Structure

```
APPRAISAL AGGREGATE
├── Appraisal (Root) ← 502 lines, manages lifecycle
│   ├── AppraisalProperty (Owned, unbounded)
│   │   ├── LandAppraisalDetail (1:1, ~60 fields)
│   │   ├── BuildingAppraisalDetail (1:1, ~40 fields)
│   │   ├── CondoAppraisalDetail (1:1)
│   │   ├── VehicleAppraisalDetail (1:1)
│   │   ├── VesselAppraisalDetail (1:1)
│   │   └── MachineryAppraisalDetail (1:1)
│   ├── PropertyGroup (Owned, unbounded)
│   │   └── PropertyGroupItem (junction to AppraisalProperty)
│   └── AppraisalAssignment (Related, unbounded)

PRICING ANALYSIS AGGREGATE (Separate)
├── PricingAnalysis (Root)
│   ├── PropertyGroupId ← FK to PropertyGroup (ISSUE)
│   └── PricingAnalysisApproach
│       └── PricingAnalysisMethod
│           ├── PricingComparableLink
│           ├── PricingCalculation
│           │   └── PricingFactorScore
│           └── PricingFinalValue
```

---

## Issue Analysis

### Issue 1: Cross-Aggregate FK Violation (MEDIUM PRIORITY)

**Problem**: `PricingAnalysis.PropertyGroupId` is a FK to `PropertyGroup`, but PropertyGroup is owned by Appraisal aggregate.

**DDD Rule**: Aggregates should reference each other by ID only, not by navigation property. However, PropertyGroup currently has no independent identity - it exists only within Appraisal.

**Why This Matters**:
- If you delete a PropertyGroup, what happens to PricingAnalysis?
- Transaction boundaries are unclear - updating PricingAnalysis shouldn't lock Appraisal
- Currently working because EF Core handles the FK constraint

**Current Behavior** (acceptable):
```csharp
// PricingAnalysis only stores the ID
public Guid PropertyGroupId { get; private set; }
// No navigation property to PropertyGroup
```

This is actually OK - you're storing an ID, not a navigation property.

### Issue 2: Appraisal Aggregate Size (LOW-MEDIUM PRIORITY)

**Size Assessment**:
| Child Entity | Est. Count per Appraisal | Concern Level |
|--------------|--------------------------|---------------|
| AppraisalProperty | 1-20 typical | Low |
| PropertyGroup | 1-5 typical | Low |
| AppraisalAssignment | 1-10 typical | Low |

**Memory Impact per Load**:
- Property with LandDetail: ~2-3KB
- Property with BuildingDetail: ~1.5KB
- 10 properties typical load: ~25KB

**Verdict**: This is acceptable for the domain. Real estate appraisals naturally have multiple properties.

### Issue 3: Deep Nesting in PricingAnalysis (LOW PRIORITY)

**Depth**: PricingAnalysis → Approach → Method → Calculation → FactorScore (5 levels)

**Why It's OK**:
- PricingAnalysis is a **separate aggregate**
- Each level is small (3 approaches max, few methods per approach)
- Represents natural structure of appraisal pricing methodology

---

## Recommendations

### Recommendation 1: Keep Current Structure (Preferred)

The current design is acceptable because:
1. PricingAnalysis references PropertyGroupId by ID only (no navigation)
2. Aggregate sizes are reasonable for the domain
3. Transaction boundaries are clear:
   - Appraisal operations: lock Appraisal aggregate
   - Pricing operations: lock PricingAnalysis aggregate

**What to document**:
```markdown
## Aggregate Boundaries
- PropertyGroup is owned by Appraisal (lifetime tied to parent)
- PricingAnalysis references PropertyGroupId (not navigation)
- Deleting PropertyGroup should cascade delete PricingAnalysis
```

### Recommendation 2: Add Cascade Delete for PropertyGroup → PricingAnalysis

**Currently missing**: When you `DeleteGroup()` on Appraisal, the related PricingAnalysis becomes orphaned.

**Solution** (in PricingConfiguration.cs or via domain event):
```csharp
// Option A: Database cascade (simple)
builder.HasOne<PropertyGroup>()
    .WithOne()
    .HasForeignKey<PricingAnalysis>(pa => pa.PropertyGroupId)
    .OnDelete(DeleteBehavior.Cascade);

// Option B: Domain event (more control)
// When PropertyGroup is deleted, publish event
// PricingAnalysis handler deletes corresponding analysis
```

### Recommendation 3: Add Collection Size Guards (Optional)

If worried about unbounded growth:

```csharp
// In Appraisal.cs
public AppraisalProperty AddProperty(...)
{
    if (_properties.Count >= 100)
        throw new InvalidOperationException("Maximum 100 properties per appraisal");
    // ...
}

public PropertyGroup CreateGroup(...)
{
    if (_groups.Count >= 20)
        throw new InvalidOperationException("Maximum 20 groups per appraisal");
    // ...
}
```

### Recommendation 4: Do NOT Extract PropertyGroup (Avoid Complexity)

**Why extraction is not recommended**:
1. PropertyGroup has no meaningful lifecycle outside Appraisal
2. Group validation (`AddPropertyToGroup`) needs access to `_properties` collection
3. Would require cross-aggregate coordination for simple operations
4. Current design correctly models domain: "Groups belong to Appraisals"

---

## Decision Matrix

| Option | Complexity | Risk | Benefit |
|--------|------------|------|---------|
| Keep as-is | None | Low | Works correctly |
| Add cascade delete | Low | Low | Prevents orphans |
| Extract PropertyGroup | High | Medium | Unclear benefit |
| Extract AppraisalProperty | Very High | High | Not recommended |

---

## Final Assessment

**Your aggregate design is sound for DDD.**

Key points:
1. **Appraisal** correctly owns **PropertyGroup** (lifecycle dependency)
2. **PricingAnalysis** correctly references by ID only (no violation)
3. Aggregate sizes are reasonable for real estate domain
4. Transaction boundaries are clear

**One action item**: Verify cascade delete from PropertyGroup to PricingAnalysis.

---

## Confirmed Issue: Missing Cascade Delete

**File**: `PricingConfiguration.cs` (lines 11-13)
```csharp
builder.Property(p => p.PropertyGroupId).IsRequired();
builder.HasIndex(p => p.PropertyGroupId).IsUnique();
// NO HasForeignKey relationship configured!
```

**Problem**: No FK relationship from `PricingAnalysis.PropertyGroupId` → `PropertyGroup`.

**Impact**: Deleting a PropertyGroup leaves PricingAnalysis orphaned with invalid FK.

**Recommended Fix** (add to `PricingAnalysisConfiguration`):
```csharp
// Option 1: Add FK with cascade delete (if PropertyGroup were independent)
// Can't do this easily because PropertyGroup is owned by Appraisal

// Option 2: Handle via application logic in Appraisal.DeleteGroup()
public void DeleteGroup(Guid groupId)
{
    // Before removing group, publish domain event
    AddDomainEvent(new PropertyGroupDeletedEvent(groupId));

    var group = _groups.FirstOrDefault(g => g.Id == groupId)
        ?? throw new InvalidOperationException($"Group {groupId} not found");
    _groups.Remove(group);
}
```

**Best Solution**: Handle orphan cleanup in application layer when deleting PropertyGroup.

---

## Checklist

- [x] Review Appraisal aggregate structure
- [x] Review PricingAnalysis aggregate structure
- [x] Analyze cross-aggregate relationships
- [x] Assess aggregate size concerns
- [x] Verify cascade delete behavior → **ISSUE FOUND**
- [ ] Implement orphan cleanup for PricingAnalysis
- [ ] Document aggregate boundaries in code

---

## Review Summary

| Area | Status | Notes |
|------|--------|-------|
| Aggregate Root Design | ✓ Good | Clear root entities |
| Entity Ownership | ✓ Good | Owned vs Related correct |
| Cross-Aggregate Reference | ✓ Acceptable | ID reference only |
| Collection Sizes | ⚠ Unbounded | Consider guards |
| Transaction Boundaries | ✓ Good | Clear separation |
| Cascade Behavior | ❌ Missing | PropertyGroup → PricingAnalysis orphan issue |

---

## Action Items

1. **HIGH**: Add orphan cleanup when deleting PropertyGroup
   - Option A: Domain event `PropertyGroupDeletedEvent` → handler deletes PricingAnalysis
   - Option B: In `DeletePropertyGroup` command handler, delete PricingAnalysis first

2. **LOW**: Consider adding collection size guards in domain

3. **DOCUMENTATION**: Add aggregate boundary documentation to codebase

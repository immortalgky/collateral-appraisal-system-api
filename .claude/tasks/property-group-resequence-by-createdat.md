# Property Group — resequence GroupNumber by CreatedAt on add/remove

## Problem
- Editor shows group label = `GroupName` (frozen "Group N" at creation).
- Backend `GroupNumber` drives ordering (`OrderBy(GroupNumber)`).
- On delete, `GroupNumber` resequences off the in-memory list order (not an explicit
  `CreatedAt` sort); on create it is just `count + 1`. This lets the displayed order drift.

## Decision (confirmed with user)
- Reapply **GroupNumber only**, ordered by `CreatedAt`. Do **not** touch `GroupName`.

## Plan
- [x] Add a private `ResequenceGroups()` helper on the `Appraisal` aggregate that orders
      `_groups` by `CreatedAt` (nulls last, so a not-yet-saved new group lands last),
      tie-broken by `Id` (Guid v7 = creation order), and assigns `GroupNumber = i + 1`.
- [x] Call it from `CreateGroup` (after `_groups.Add`).
- [x] Call it from `DeleteGroup` (replacing the inline resequence loop).
- [x] No EF/migration change needed — `(AppraisalId, GroupNumber)` index is already non-unique.

## Review
- Change is isolated to `Modules/Appraisal/Appraisal/Domain/Appraisals/Appraisal.cs`.
- Existing drifted data self-heals on the next add/remove of a group for that appraisal.
- `GetPropertyGroupsQueryHandler` already returns `OrderBy(GroupNumber)`, so display order
  follows the corrected numbering automatically.

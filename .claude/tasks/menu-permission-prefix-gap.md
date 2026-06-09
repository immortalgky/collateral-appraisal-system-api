# M3 Menu Permission Prefix Gap

## Goal
Add `ViewPermissionPrefix` (nullable string) to `MenuItem` so a user holding ANY permission
starting with that prefix sees the menu node — fixing monitoring nodes that were gated to only
the `:staff` layer permission.

## Plan

- [x] 1. `MenuItem.cs` — add `ViewPermissionPrefix` property; update `Create` + `Update` to accept it;
         relax the required-check: at least one of (code, prefix) must be non-empty. When only prefix
         is supplied, code is stored as `string.Empty` (satisfies NOT NULL DB constraint).
- [x] 2. `MenuItemConfiguration.cs` — map new nullable `nvarchar(200)` column.
- [x] 3. Run EF migration `AddMenuItemViewPermissionPrefix` — single ADD COLUMN, no data loss.
- [x] 4. `GetMyMenuQueryHandler.cs` — extend visibility check with `StartsWith` prefix match.
- [x] 5. `CreateMenuItemCommand.cs` + `CreateMenuItemCommandValidator.cs` + `CreateMenuItemCommandHandler.cs` — accept + pass through `ViewPermissionPrefix`; validator uses Must() cross-field rule.
- [x] 6. `UpdateMenuItemCommand.cs` + `UpdateMenuItemCommandValidator.cs` + `UpdateMenuItemCommandHandler.cs` — same.
- [x] 7. `CreateMenuItemRequest.cs` + `UpdateMenuItemRequest.cs` — expose new field (Mapster adapts by name).
- [x] 8. `MenuItemAdminDto.cs` + `GetMenuItemsQueryHandler.cs` — expose + map new field in DTO.
- [x] 9. `MenuSeedData.cs` — add `ViewPermissionPrefix` param to `MenuSeedNode`; updated:
         - `main.monitoring` parent: `ViewPermissionPrefix = "monitoring:"`
         - `main.monitoring.pending-internal`: prefix `"monitoring:pending-internal:"`, code cleared.
         - `main.monitoring.pending-external`: prefix `"monitoring:pending-external:"`, code cleared.
         - Removed M3 TODO comment block.
- [x] 10. `AuthDataSeed.cs` / `UpsertTreeAsync` — pass prefix through to `MenuItem.Create`; added
          backfill for existing rows where `ViewPermissionPrefix IS NULL` and seed now defines one.
- [x] 11. `dotnet build` — green, 0 errors.
- [x] 12. Migration verified: single `AddColumn<string>(nullable: true)`.

## Review

Files changed:
- `Modules/Auth/Auth/Domain/Menu/MenuItem.cs`
- `Modules/Auth/Auth/Infrastructure/Configurations/MenuItemConfiguration.cs`
- `Modules/Auth/Auth/Infrastructure/Migrations/20260520111802_AddMenuItemViewPermissionPrefix.cs` (generated)
- `Modules/Auth/Auth/Application/Features/Menu/GetMyMenu/GetMyMenuQueryHandler.cs`
- `Modules/Auth/Auth/Application/Features/Menu/CreateMenuItem/CreateMenuItemCommand.cs`
- `Modules/Auth/Auth/Application/Features/Menu/CreateMenuItem/CreateMenuItemCommandValidator.cs`
- `Modules/Auth/Auth/Application/Features/Menu/CreateMenuItem/CreateMenuItemCommandHandler.cs`
- `Modules/Auth/Auth/Application/Features/Menu/CreateMenuItem/CreateMenuItemRequest.cs`
- `Modules/Auth/Auth/Application/Features/Menu/UpdateMenuItem/UpdateMenuItemCommand.cs`
- `Modules/Auth/Auth/Application/Features/Menu/UpdateMenuItem/UpdateMenuItemCommandValidator.cs`
- `Modules/Auth/Auth/Application/Features/Menu/UpdateMenuItem/UpdateMenuItemCommandHandler.cs`
- `Modules/Auth/Auth/Application/Features/Menu/UpdateMenuItem/UpdateMenuItemRequest.cs`
- `Modules/Auth/Auth/Application/Features/Menu/Dtos/MenuItemAdminDto.cs`
- `Modules/Auth/Auth/Application/Features/Menu/GetMenuItems/GetMenuItemsQueryHandler.cs`
- `Modules/Auth/Auth/Infrastructure/Seed/MenuSeedData.cs`
- `Modules/Auth/Auth/Infrastructure/Seed/AuthDataSeed.cs`

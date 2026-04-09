# NAS Storage Integration with Configurable Switch

## Tasks

- [x] 1. Add `StorageMode` enum and new properties to `FileStorageConfiguration`
- [x] 2. Update `DocumentService` to resolve base path by mode
- [x] 3. Swap static file provider based on mode in `Program.cs` + startup validation
- [x] 4. Add `Mode` and `NasBasePath` to `appsettings.json`
- [x] 5. Build verification (`dotnet build`) — 0 errors

## Review

### What Changed (4 files)

**1. `Shared/Shared/Configurations/FileStorageConfiguration.cs`**
- Added `StorageMode` enum (`Local`, `Nas`) at namespace level.
- Added `Mode` property (defaults to `Local`) and nullable `NasBasePath` property.

**2. `Modules/Document/Document/Application/Services/DocumentService.cs`**
- Added `GetStorageBasePath()` helper that returns `NasBasePath` when mode is `Nas` (and path is set), otherwise `webHostEnvironment.WebRootPath`.
- `UploadAsync` now calls this helper instead of hardcoding `WebRootPath`.
- `StorageUrl` is unchanged — both modes produce `/uploads/documents/{filename}`.

**3. `Bootstrapper/Api/Program.cs`**
- Reads `FileStorageConfiguration` from config.
- If NAS mode: validates `Directory.Exists(NasBasePath)`. If accessible, serves static files from NAS via `PhysicalFileProvider`. If not accessible, logs a warning and falls back to default `UseStaticFiles()`.
- If Local mode: standard `UseStaticFiles()` (unchanged behavior).
- The `/Assets` static file provider remains untouched.

**4. `Bootstrapper/Api/appsettings.json`**
- Added `"Mode": "Local"` and `"NasBasePath": "\\\\nas_app_dev\\CAS"` to the `FileStorage` section.

### Security
- No credentials stored in config — NAS access relies on OS-level Windows service account.
- No new user inputs introduced.
- No raw SQL or string interpolation added.
- `NasBasePath` is only used in `Path.Combine` and `Directory.Exists` — no path traversal risk since it's admin-controlled config, not user input.

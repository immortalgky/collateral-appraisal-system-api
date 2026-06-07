# SystemConfiguration Table — Phase 0

## Plan

- [x] 1. Entity: `Modules/Common/Common/Domain/Configuration/SystemConfiguration.cs`
- [x] 2. EF config: `Modules/Common/Common/Infrastructure/Configurations/SystemConfigurationConfiguration.cs`
- [x] 3. Add DbSet to `CommonDbContext`
- [x] 4. Interface: `Shared/Shared/Configuration/ISystemConfigurationReader.cs`
- [x] 5. Implementation: `Modules/Common/Common/Infrastructure/Configuration/SystemConfigurationReader.cs`
- [x] 6. Query: `GetSystemConfigurationsQuery` + handler
- [x] 7. Query: `GetSystemConfigurationByKeyQuery` + handler
- [x] 8. Command: `UpdateSystemConfigurationCommand` + handler
- [x] 9. Endpoint: `SystemConfigurationEndpoints` (Carter ICarterModule)
- [x] 10. Seeder: `SystemConfigurationDataSeed`
- [x] 11. Register reader + seeder in `CommonModule`
- [x] 12. EF migration (hand-authored): `AddSystemConfiguration`
- [x] 13. Update model snapshot
- [x] 14. `dotnet build` — 0 errors confirmed

## Review

Build: 30 projects, 0 errors, 544 warnings (all pre-existing).
Migration `20260603120000_AddSystemConfiguration` confirmed Pending by `dotnet ef migrations list`.

Files created/modified:
- Domain/Configuration/SystemConfiguration.cs (entity, factory, mutators)
- Infrastructure/Configurations/SystemConfigurationConfiguration.cs (EF config, unique index on Key)
- Infrastructure/CommonDbContext.cs (DbSet added)
- Infrastructure/Configuration/SystemConfigurationReader.cs (IMemoryCache, 60s TTL, parse+fallback)
- Infrastructure/Seed/SystemConfigurationDataSeed.cs (guarded per-key insert)
- Application/Features/SystemConfiguration/* (DTO, 3 queries/commands, 3 handlers, Carter endpoint)
- Shared/Shared/Configuration/ISystemConfigurationReader.cs (cross-module contract)
- CommonModule.cs (services.AddMemoryCache, ISystemConfigurationReader, IDataSeeder<CommonDbContext>)
- Migrations/20260603120000_AddSystemConfiguration.cs + .Designer.cs
- Migrations/CommonDbContextModelSnapshot.cs (SystemConfiguration entity block added)

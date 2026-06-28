IF OBJECT_ID(N'[auth].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'auth') IS NULL EXEC(N'CREATE SCHEMA [auth];');
    CREATE TABLE [auth].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    IF SCHEMA_ID(N'auth') IS NULL EXEC(N'CREATE SCHEMA [auth];');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [AvatarUrl] nvarchar(500) NULL,
        [Position] nvarchar(100) NULL,
        [Department] nvarchar(100) NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[OpenIddictApplications] (
        [Id] nvarchar(450) NOT NULL,
        [ApplicationType] nvarchar(50) NULL,
        [ClientId] nvarchar(100) NULL,
        [ClientSecret] nvarchar(max) NULL,
        [ClientType] nvarchar(50) NULL,
        [ConcurrencyToken] nvarchar(50) NULL,
        [ConsentType] nvarchar(50) NULL,
        [DisplayName] nvarchar(max) NULL,
        [DisplayNames] nvarchar(max) NULL,
        [JsonWebKeySet] nvarchar(max) NULL,
        [Permissions] nvarchar(max) NULL,
        [PostLogoutRedirectUris] nvarchar(max) NULL,
        [Properties] nvarchar(max) NULL,
        [RedirectUris] nvarchar(max) NULL,
        [Requirements] nvarchar(max) NULL,
        [Settings] nvarchar(max) NULL,
        CONSTRAINT [PK_OpenIddictApplications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[OpenIddictScopes] (
        [Id] nvarchar(450) NOT NULL,
        [ConcurrencyToken] nvarchar(50) NULL,
        [Description] nvarchar(max) NULL,
        [Descriptions] nvarchar(max) NULL,
        [DisplayName] nvarchar(max) NULL,
        [DisplayNames] nvarchar(max) NULL,
        [Name] nvarchar(200) NULL,
        [Properties] nvarchar(max) NULL,
        [Resources] nvarchar(max) NULL,
        CONSTRAINT [PK_OpenIddictScopes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[Permissions] (
        [PermissionId] uniqueidentifier NOT NULL,
        [PermissionCode] nvarchar(450) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(10) NULL,
        [CreatedWorkstation] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(10) NULL,
        [UpdatedWorkstation] nvarchar(max) NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([PermissionId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [auth].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [auth].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [auth].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [auth].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [auth].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [auth].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[OpenIddictAuthorizations] (
        [Id] nvarchar(450) NOT NULL,
        [ApplicationId] nvarchar(450) NULL,
        [ConcurrencyToken] nvarchar(50) NULL,
        [CreationDate] datetime2 NULL,
        [Properties] nvarchar(max) NULL,
        [Scopes] nvarchar(max) NULL,
        [Status] nvarchar(50) NULL,
        [Subject] nvarchar(400) NULL,
        [Type] nvarchar(50) NULL,
        CONSTRAINT [PK_OpenIddictAuthorizations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OpenIddictAuthorizations_OpenIddictApplications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [auth].[OpenIddictApplications] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[RolePermissions] (
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [auth].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [auth].[Permissions] ([PermissionId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[UserPermissions] (
        [UserId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [IsGranted] bit NOT NULL,
        CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([UserId], [PermissionId]),
        CONSTRAINT [FK_UserPermissions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [auth].[AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserPermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [auth].[Permissions] ([PermissionId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE TABLE [auth].[OpenIddictTokens] (
        [Id] nvarchar(450) NOT NULL,
        [ApplicationId] nvarchar(450) NULL,
        [AuthorizationId] nvarchar(450) NULL,
        [ConcurrencyToken] nvarchar(50) NULL,
        [CreationDate] datetime2 NULL,
        [ExpirationDate] datetime2 NULL,
        [Payload] nvarchar(max) NULL,
        [Properties] nvarchar(max) NULL,
        [RedemptionDate] datetime2 NULL,
        [ReferenceId] nvarchar(100) NULL,
        [Status] nvarchar(50) NULL,
        [Subject] nvarchar(400) NULL,
        [Type] nvarchar(50) NULL,
        CONSTRAINT [PK_OpenIddictTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OpenIddictTokens_OpenIddictApplications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [auth].[OpenIddictApplications] ([Id]),
        CONSTRAINT [FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId] FOREIGN KEY ([AuthorizationId]) REFERENCES [auth].[OpenIddictAuthorizations] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [auth].[AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [auth].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [auth].[AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [auth].[AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [auth].[AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [auth].[AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [auth].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OpenIddictApplications_ClientId] ON [auth].[OpenIddictApplications] ([ClientId]) WHERE [ClientId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type] ON [auth].[OpenIddictAuthorizations] ([ApplicationId], [Status], [Subject], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OpenIddictScopes_Name] ON [auth].[OpenIddictScopes] ([Name]) WHERE [Name] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [IX_OpenIddictTokens_ApplicationId_Status_Subject_Type] ON [auth].[OpenIddictTokens] ([ApplicationId], [Status], [Subject], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [IX_OpenIddictTokens_AuthorizationId] ON [auth].[OpenIddictTokens] ([AuthorizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OpenIddictTokens_ReferenceId] ON [auth].[OpenIddictTokens] ([ReferenceId]) WHERE [ReferenceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_PermissionCode] ON [auth].[Permissions] ([PermissionCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [auth].[RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    CREATE INDEX [IX_UserPermissions_PermissionId] ON [auth].[UserPermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209071336_Initial'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260209071336_Initial', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260314101901_AddCompanyAndUserCompanyId'
)
BEGIN
    ALTER TABLE [auth].[AspNetUsers] ADD [CompanyId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260314101901_AddCompanyAndUserCompanyId'
)
BEGIN
    CREATE TABLE [auth].[Companies] (
        [CompanyId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [TaxId] nvarchar(50) NULL,
        [Phone] nvarchar(50) NULL,
        [Email] nvarchar(200) NULL,
        [Street] nvarchar(500) NULL,
        [City] nvarchar(100) NULL,
        [Province] nvarchar(100) NULL,
        [PostalCode] nvarchar(20) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedOn] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [CreatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(10) NULL,
        [CreatedWorkstation] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(10) NULL,
        [UpdatedWorkstation] nvarchar(max) NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([CompanyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260314101901_AddCompanyAndUserCompanyId'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Companies_Name] ON [auth].[Companies] ([Name]) WHERE IsDeleted = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260314101901_AddCompanyAndUserCompanyId'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260314101901_AddCompanyAndUserCompanyId', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320024414_AddAuthSourceToApplicationUser'
)
BEGIN
    ALTER TABLE [auth].[AspNetUsers] ADD [AuthSource] nvarchar(max) NOT NULL DEFAULT N'Local';
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320024414_AddAuthSourceToApplicationUser'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320024414_AddAuthSourceToApplicationUser', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260323163035_AddLoanTypeToCompany'
)
BEGIN
    ALTER TABLE [auth].[Companies] ADD [LoanTypes] nvarchar(max) NOT NULL DEFAULT ('[]');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260323163035_AddLoanTypeToCompany'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260323163035_AddLoanTypeToCompany', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260323164333_RenameCompanyPkToId'
)
BEGIN
    EXEC sp_rename N'[auth].[Companies].[CompanyId]', N'Id', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260323164333_RenameCompanyPkToId'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260323164333_RenameCompanyPkToId', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326072844_AddDisplayNameAndModuleToPermission'
)
BEGIN
    DROP INDEX [IX_Permissions_PermissionCode] ON [auth].[Permissions];
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[auth].[Permissions]') AND [c].[name] = N'PermissionCode');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [auth].[Permissions] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [auth].[Permissions] ALTER COLUMN [PermissionCode] nvarchar(100) NOT NULL;
    CREATE UNIQUE INDEX [IX_Permissions_PermissionCode] ON [auth].[Permissions] ([PermissionCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326072844_AddDisplayNameAndModuleToPermission'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[auth].[Permissions]') AND [c].[name] = N'Description');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [auth].[Permissions] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [auth].[Permissions] ALTER COLUMN [Description] nvarchar(500) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326072844_AddDisplayNameAndModuleToPermission'
)
BEGIN
    ALTER TABLE [auth].[Permissions] ADD [DisplayName] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326072844_AddDisplayNameAndModuleToPermission'
)
BEGIN
    ALTER TABLE [auth].[Permissions] ADD [Module] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326072844_AddDisplayNameAndModuleToPermission'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260326072844_AddDisplayNameAndModuleToPermission', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326073851_AddScopeToApplicationRole'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[auth].[AspNetRoles]') AND [c].[name] = N'Description');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [auth].[AspNetRoles] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [auth].[AspNetRoles] ALTER COLUMN [Description] nvarchar(500) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326073851_AddScopeToApplicationRole'
)
BEGIN
    ALTER TABLE [auth].[AspNetRoles] ADD [Scope] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326073851_AddScopeToApplicationRole'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260326073851_AddScopeToApplicationRole', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326074740_AddGroupTables'
)
BEGIN
    CREATE TABLE [auth].[Groups] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Scope] nvarchar(50) NOT NULL,
        [CompanyId] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedOn] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [CreatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(10) NULL,
        [CreatedWorkstation] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(10) NULL,
        [UpdatedWorkstation] nvarchar(max) NULL,
        CONSTRAINT [PK_Groups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326074740_AddGroupTables'
)
BEGIN
    CREATE TABLE [auth].[GroupMonitoring] (
        [MonitorGroupId] uniqueidentifier NOT NULL,
        [MonitoredGroupId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_GroupMonitoring] PRIMARY KEY ([MonitorGroupId], [MonitoredGroupId]),
        CONSTRAINT [FK_GroupMonitoring_Groups_MonitorGroupId] FOREIGN KEY ([MonitorGroupId]) REFERENCES [auth].[Groups] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GroupMonitoring_Groups_MonitoredGroupId] FOREIGN KEY ([MonitoredGroupId]) REFERENCES [auth].[Groups] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326074740_AddGroupTables'
)
BEGIN
    CREATE TABLE [auth].[GroupUsers] (
        [GroupId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_GroupUsers] PRIMARY KEY ([GroupId], [UserId]),
        CONSTRAINT [FK_GroupUsers_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [auth].[Groups] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326074740_AddGroupTables'
)
BEGIN
    CREATE INDEX [IX_GroupMonitoring_MonitoredGroupId] ON [auth].[GroupMonitoring] ([MonitoredGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326074740_AddGroupTables'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Groups_Name_Scope] ON [auth].[Groups] ([Name], [Scope]) WHERE IsDeleted = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326074740_AddGroupTables'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260326074740_AddGroupTables', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330073524_AddContactPersonToCompany'
)
BEGIN
    ALTER TABLE [auth].[Companies] ADD [ContactPerson] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260330073524_AddContactPersonToCompany'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260330073524_AddContactPersonToCompany', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409144134_AddMenuItems'
)
BEGIN
    CREATE TABLE [auth].[MenuItems] (
        [MenuItemId] uniqueidentifier NOT NULL,
        [ItemKey] nvarchar(200) NOT NULL,
        [Scope] int NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [Path] nvarchar(500) NULL,
        [IconName] nvarchar(100) NOT NULL,
        [IconStyle] int NOT NULL,
        [IconColor] nvarchar(100) NULL,
        [SortOrder] int NOT NULL,
        [ViewPermissionCode] nvarchar(100) NOT NULL,
        [EditPermissionCode] nvarchar(100) NULL,
        [IsSystem] bit NOT NULL,
        [CreatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(10) NULL,
        [CreatedWorkstation] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(10) NULL,
        [UpdatedWorkstation] nvarchar(max) NULL,
        CONSTRAINT [PK_MenuItems] PRIMARY KEY ([MenuItemId]),
        CONSTRAINT [FK_MenuItems_MenuItems_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [auth].[MenuItems] ([MenuItemId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409144134_AddMenuItems'
)
BEGIN
    CREATE TABLE [auth].[MenuItemTranslations] (
        [MenuItemId] uniqueidentifier NOT NULL,
        [LanguageCode] nvarchar(10) NOT NULL,
        [Label] nvarchar(500) NOT NULL,
        [CreatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(10) NULL,
        [CreatedWorkstation] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(10) NULL,
        [UpdatedWorkstation] nvarchar(max) NULL,
        CONSTRAINT [PK_MenuItemTranslations] PRIMARY KEY ([MenuItemId], [LanguageCode]),
        CONSTRAINT [FK_MenuItemTranslations_MenuItems_MenuItemId] FOREIGN KEY ([MenuItemId]) REFERENCES [auth].[MenuItems] ([MenuItemId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409144134_AddMenuItems'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MenuItems_ItemKey] ON [auth].[MenuItems] ([ItemKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409144134_AddMenuItems'
)
BEGIN
    CREATE INDEX [IX_MenuItems_ParentId] ON [auth].[MenuItems] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409144134_AddMenuItems'
)
BEGIN
    CREATE INDEX [IX_MenuItems_Scope_ParentId_SortOrder] ON [auth].[MenuItems] ([Scope], [ParentId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409144134_AddMenuItems'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_MenuItems_Scope_Path] ON [auth].[MenuItems] ([Scope], [Path]) WHERE [Path] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409144134_AddMenuItems'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409144134_AddMenuItems', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419131228_AddActivityMenuOverrides'
)
BEGIN
    CREATE TABLE [auth].[ActivityMenuOverrides] (
        [ActivityMenuOverrideId] uniqueidentifier NOT NULL,
        [ActivityId] nvarchar(100) NOT NULL,
        [MenuItemId] uniqueidentifier NOT NULL,
        [IsVisible] bit NOT NULL,
        [CanEdit] bit NOT NULL,
        [CreatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(10) NULL,
        [CreatedWorkstation] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(10) NULL,
        [UpdatedWorkstation] nvarchar(max) NULL,
        CONSTRAINT [PK_ActivityMenuOverrides] PRIMARY KEY ([ActivityMenuOverrideId]),
        CONSTRAINT [FK_ActivityMenuOverrides_MenuItems_MenuItemId] FOREIGN KEY ([MenuItemId]) REFERENCES [auth].[MenuItems] ([MenuItemId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419131228_AddActivityMenuOverrides'
)
BEGIN
    CREATE INDEX [IX_ActivityMenuOverrides_ActivityId] ON [auth].[ActivityMenuOverrides] ([ActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419131228_AddActivityMenuOverrides'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ActivityMenuOverrides_ActivityId_MenuItemId] ON [auth].[ActivityMenuOverrides] ([ActivityId], [MenuItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419131228_AddActivityMenuOverrides'
)
BEGIN
    CREATE INDEX [IX_ActivityMenuOverrides_MenuItemId] ON [auth].[ActivityMenuOverrides] ([MenuItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419131228_AddActivityMenuOverrides'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419131228_AddActivityMenuOverrides', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513141402_AddCompanyBankAccountFields'
)
BEGIN
    ALTER TABLE [auth].[Companies] ADD [BankAccountName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513141402_AddCompanyBankAccountFields'
)
BEGIN
    ALTER TABLE [auth].[Companies] ADD [BankAccountNo] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513141402_AddCompanyBankAccountFields'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513141402_AddCompanyBankAccountFields', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516214855_AddDataProtectionKeys'
)
BEGIN
    CREATE TABLE [auth].[DataProtectionKeys] (
        [Id] int NOT NULL IDENTITY,
        [FriendlyName] nvarchar(max) NULL,
        [Xml] nvarchar(max) NULL,
        CONSTRAINT [PK_DataProtectionKeys] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516214855_AddDataProtectionKeys'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516214855_AddDataProtectionKeys', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520111802_AddMenuItemViewPermissionPrefix'
)
BEGIN
    ALTER TABLE [auth].[MenuItems] ADD [ViewPermissionPrefix] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520111802_AddMenuItemViewPermissionPrefix'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260520111802_AddMenuItemViewPermissionPrefix', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520114933_MakeMenuItemViewPermissionCodeNullable'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[auth].[MenuItems]') AND [c].[name] = N'ViewPermissionCode');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [auth].[MenuItems] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [auth].[MenuItems] ALTER COLUMN [ViewPermissionCode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520114933_MakeMenuItemViewPermissionCodeNullable'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260520114933_MakeMenuItemViewPermissionCodeNullable', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521154232_AddUserPreferences'
)
BEGIN
    CREATE TABLE [auth].[UserPreferences] (
        [UserId] uniqueidentifier NOT NULL,
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(max) NOT NULL,
        [UpdatedOn] datetime2 NOT NULL,
        CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([UserId], [Key]),
        CONSTRAINT [FK_UserPreferences_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [auth].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521154232_AddUserPreferences'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260521154232_AddUserPreferences', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604030415_AddAuthAuditLog'
)
BEGIN
    ALTER TABLE [auth].[AspNetUsers] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604030415_AddAuthAuditLog'
)
BEGIN
    ALTER TABLE [auth].[AspNetUsers] ADD [LastLoginAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604030415_AddAuthAuditLog'
)
BEGIN
    ALTER TABLE [auth].[AspNetUsers] ADD [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604030415_AddAuthAuditLog'
)
BEGIN
    CREATE TABLE [auth].[AuthAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [ActorName] nvarchar(256) NULL,
        [Action] nvarchar(50) NOT NULL,
        [EntityType] nvarchar(50) NOT NULL,
        [EntityId] uniqueidentifier NULL,
        [EntityName] nvarchar(256) NULL,
        [ChangesJson] nvarchar(max) NULL,
        [Workstation] nvarchar(256) NULL,
        [IpAddress] nvarchar(64) NULL,
        [CreatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(10) NULL,
        [CreatedWorkstation] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(10) NULL,
        [UpdatedWorkstation] nvarchar(max) NULL,
        CONSTRAINT [PK_AuthAuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604030415_AddAuthAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuthAuditLogs_ActorUserId] ON [auth].[AuthAuditLogs] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604030415_AddAuthAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuthAuditLogs_EntityType_EntityId_OccurredAt] ON [auth].[AuthAuditLogs] ([EntityType], [EntityId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604030415_AddAuthAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuthAuditLogs_OccurredAt] ON [auth].[AuthAuditLogs] ([OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604030415_AddAuthAuditLog'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260604030415_AddAuthAuditLog', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608054656_AddAspNetUsersUserNameIndex'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_UserName] ON [auth].[AspNetUsers] ([UserName]) INCLUDE ([FirstName], [LastName]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608054656_AddAspNetUsersUserNameIndex'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260608054656_AddAspNetUsersUserNameIndex', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608111858_AddHostCompanyCodeToCompany'
)
BEGIN
    ALTER TABLE [auth].[Companies] ADD [HostCompanyCode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608111858_AddHostCompanyCodeToCompany'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260608111858_AddHostCompanyCodeToCompany', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610063603_ConstrainAuthSourceLength'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[auth].[AspNetUsers]') AND [c].[name] = N'AuthSource');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [auth].[AspNetUsers] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [auth].[AspNetUsers] ALTER COLUMN [AuthSource] nvarchar(20) NOT NULL;
    ALTER TABLE [auth].[AspNetUsers] ADD DEFAULT N'Local' FOR [AuthSource];
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610063603_ConstrainAuthSourceLength'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610063603_ConstrainAuthSourceLength', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610094500_RenameTeamTypeToScopeDropIsActive'
)
BEGIN

    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'Type')
       AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'Scope')
    BEGIN
        DECLARE @df sysname;
        SELECT @df = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('auth.Teams') AND c.name = 'Type';
        IF @df IS NOT NULL EXEC('ALTER TABLE auth.Teams DROP CONSTRAINT ' + @df);
        EXEC sp_rename 'auth.Teams.Type', 'Scope', 'COLUMN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610094500_RenameTeamTypeToScopeDropIsActive'
)
BEGIN

    UPDATE auth.Teams
    SET Scope = CASE Scope WHEN 'Internal' THEN 'Bank' WHEN 'External' THEN 'Company' ELSE Scope END
    WHERE Scope IN ('Internal', 'External');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610094500_RenameTeamTypeToScopeDropIsActive'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('auth.Teams') AND c.name = 'Scope')
        ALTER TABLE auth.Teams ADD CONSTRAINT DF_Teams_Scope DEFAULT 'Bank' FOR Scope;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610094500_RenameTeamTypeToScopeDropIsActive'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'Description')
        ALTER TABLE auth.Teams ADD Description NVARCHAR(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610094500_RenameTeamTypeToScopeDropIsActive'
)
BEGIN

    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'IsActive')
    BEGIN
        DECLARE @dfa sysname;
        SELECT @dfa = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('auth.Teams') AND c.name = 'IsActive';
        IF @dfa IS NOT NULL EXEC('ALTER TABLE auth.Teams DROP CONSTRAINT ' + @dfa);
        ALTER TABLE auth.Teams DROP COLUMN IsActive;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610094500_RenameTeamTypeToScopeDropIsActive'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610094500_RenameTeamTypeToScopeDropIsActive', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610134720_AddPasswordPolicyAndHistory'
)
BEGIN
    ALTER TABLE [auth].[AspNetUsers] ADD [PasswordChangedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610134720_AddPasswordPolicyAndHistory'
)
BEGIN
    CREATE TABLE [auth].[PasswordHistory] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PasswordHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PasswordHistory_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [auth].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610134720_AddPasswordPolicyAndHistory'
)
BEGIN
    CREATE TABLE [auth].[PasswordPolicy] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWSEQUENTIALID()),
        [RequiredLength] int NOT NULL,
        [RequireDigit] bit NOT NULL,
        [RequireLowercase] bit NOT NULL,
        [RequireUppercase] bit NOT NULL,
        [RequireNonAlphanumeric] bit NOT NULL,
        [RequiredUniqueChars] int NOT NULL,
        [ExpiryDays] int NOT NULL,
        [HistoryCount] int NOT NULL,
        [Blocklist] nvarchar(max) NOT NULL DEFAULT N'',
        [LockoutEnabled] bit NOT NULL,
        [MaxFailedAccessAttempts] int NOT NULL,
        [LockoutMinutes] int NOT NULL,
        CONSTRAINT [PK_PasswordPolicy] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610134720_AddPasswordPolicyAndHistory'
)
BEGIN
    CREATE INDEX [IX_PasswordHistory_UserId_CreatedAt] ON [auth].[PasswordHistory] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610134720_AddPasswordPolicyAndHistory'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610134720_AddPasswordPolicyAndHistory', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611021918_AddUniqueEmailIndex'
)
BEGIN
    DROP INDEX [EmailIndex] ON [auth].[AspNetUsers];
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611021918_AddUniqueEmailIndex'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [EmailIndex] ON [auth].[AspNetUsers] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [auth].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611021918_AddUniqueEmailIndex'
)
BEGIN
    INSERT INTO [auth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260611021918_AddUniqueEmailIndex', N'9.0.8');
END;

COMMIT;
GO


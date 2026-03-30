-- ============================================================
-- Seed: Workflow Roles, Internal Users, External Users (with CompanyId)
-- Idempotent — safe to re-run.
-- ============================================================

SET NOCOUNT ON;

-- Pre-computed ASP.NET Identity v3 password hash for 'P@ssw0rd!'
DECLARE @PasswordHash NVARCHAR(MAX) = N'AQAAAAIAAYagAAAAELSuLhRxW3mqiYFaBpMSKedl0YVz3qbMiwJ8bK+k5bHp7BN4XlYSCgpJiHAhj2M/WA==';
DECLARE @SecurityStamp NVARCHAR(MAX);
DECLARE @ConcurrencyStamp NVARCHAR(MAX);
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

-- ============================================================
-- Section 1: Roles (10 roles)
-- ============================================================

DECLARE @Roles TABLE (Name NVARCHAR(256), Description NVARCHAR(500));
INSERT INTO @Roles (Name, Description) VALUES
    (N'IntAdmin',               N'Workflow administrator'),
    (N'ExtAdmin',            N'External company administrator'),
    (N'ExtAppraisalStaff',   N'External appraisal staff'),
    (N'ExtAppraisalChecker', N'External appraisal checker'),
    (N'ExtAppraisalVerifier',N'External appraisal verifier'),
    (N'IntAppraisalStaff',   N'Internal appraisal staff'),
    (N'IntAppraisalChecker', N'Internal appraisal checker'),
    (N'IntAppraisalVerifier',N'Internal appraisal verifier'),
    (N'AppraisalCommittee',  N'Appraisal committee member'),
    (N'RequestMaker',        N'Request maker / originator');

INSERT INTO auth.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description)
SELECT NEWID(), r.Name, UPPER(r.Name), NEWID(), r.Description
FROM @Roles r
WHERE NOT EXISTS (
    SELECT 1 FROM auth.AspNetRoles ar WHERE ar.NormalizedName = UPPER(r.Name)
);

-- ============================================================
-- Section 2: Internal Users (no CompanyId)
-- ============================================================

DECLARE @InternalUsers TABLE (
    UserName NVARCHAR(256), Email NVARCHAR(256),
    FirstName NVARCHAR(100), LastName NVARCHAR(100),
    Position NVARCHAR(100), Department NVARCHAR(100),
    RoleName NVARCHAR(256)
);

INSERT INTO @InternalUsers VALUES
    -- IntAppraisalStaff (3)
    (N'int.staff1',    N'int.staff1@bank.co.th',    N'Anuwat',    N'Srisawat',    N'Internal Appraiser',      N'Appraisal', N'IntAppraisalStaff'),
    (N'int.staff2',    N'int.staff2@bank.co.th',    N'Boonchu',   N'Chankaew',    N'Internal Appraiser',      N'Appraisal', N'IntAppraisalStaff'),
    (N'int.staff3',    N'int.staff3@bank.co.th',    N'Chaiyaporn',N'Duangsri',    N'Internal Appraiser',      N'Appraisal', N'IntAppraisalStaff'),
    -- IntAppraisalChecker (2)
    (N'int.chk1',      N'int.chk1@bank.co.th',     N'Damrong',   N'Ekachai',     N'Internal Checker',        N'Appraisal', N'IntAppraisalChecker'),
    (N'int.chk2',      N'int.chk2@bank.co.th',     N'Ekkalak',   N'Fuangfu',     N'Internal Checker',        N'Appraisal', N'IntAppraisalChecker'),
    -- IntAppraisalVerifier (2)
    (N'int.vrf1',      N'int.vrf1@bank.co.th',     N'Gritsada',  N'Homsuwan',    N'Internal Verifier',       N'Appraisal', N'IntAppraisalVerifier'),
    (N'int.vrf2',      N'int.vrf2@bank.co.th',     N'Itthipat',  N'Jantarakul',  N'Internal Verifier',       N'Appraisal', N'IntAppraisalVerifier'),
    -- AppraisalCommittee (2)
    (N'committee1',    N'committee1@bank.co.th',    N'Kamolchai',  N'Lertsiri',    N'Committee Member',        N'Management',N'AppraisalCommittee'),
    (N'committee2',    N'committee2@bank.co.th',    N'Mongkol',    N'Nakornprasit',N'Committee Member',        N'Management',N'AppraisalCommittee'),
    -- RequestMaker (2)
    (N'reqmaker1',     N'reqmaker1@bank.co.th',     N'Nopparat',   N'Onlamai',     N'Loan Officer',            N'Lending',   N'RequestMaker'),
    (N'reqmaker2',     N'reqmaker2@bank.co.th',     N'Ornuma',     N'Panyarat',    N'Loan Officer',            N'Lending',   N'RequestMaker');

-- Insert internal users
DECLARE @IU_UserName NVARCHAR(256), @IU_Email NVARCHAR(256),
        @IU_FirstName NVARCHAR(100), @IU_LastName NVARCHAR(100),
        @IU_Position NVARCHAR(100), @IU_Department NVARCHAR(100),
        @IU_RoleName NVARCHAR(256);

DECLARE cur_internal CURSOR LOCAL FAST_FORWARD FOR
    SELECT UserName, Email, FirstName, LastName, Position, Department, RoleName FROM @InternalUsers;

OPEN cur_internal;
FETCH NEXT FROM cur_internal INTO @IU_UserName, @IU_Email, @IU_FirstName, @IU_LastName, @IU_Position, @IU_Department, @IU_RoleName;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM auth.AspNetUsers WHERE NormalizedUserName = UPPER(@IU_UserName))
    BEGIN
        SET @SecurityStamp = UPPER(NEWID());
        SET @ConcurrencyStamp = CAST(NEWID() AS NVARCHAR(36));

        DECLARE @IU_UserId UNIQUEIDENTIFIER = NEWID();

        INSERT INTO auth.AspNetUsers (
            Id, UserName, NormalizedUserName, Email, NormalizedEmail,
            EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
            PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
            FirstName, LastName, Position, Department,
            AvatarUrl
        ) VALUES (
            @IU_UserId, @IU_UserName, UPPER(@IU_UserName), @IU_Email, UPPER(@IU_Email),
            1, @PasswordHash, @SecurityStamp, @ConcurrencyStamp,
            0, 0, 1, 0,
            @IU_FirstName, @IU_LastName, @IU_Position, @IU_Department,
            N'https://ui-avatars.com/api/?name=' + @IU_FirstName + N'+' + @IU_LastName + N'&background=3b82f6&color=fff&size=128'
        );

        -- Assign role
        INSERT INTO auth.AspNetUserRoles (UserId, RoleId)
        SELECT @IU_UserId, r.Id
        FROM auth.AspNetRoles r
        WHERE r.NormalizedName = UPPER(@IU_RoleName);
    END

    FETCH NEXT FROM cur_internal INTO @IU_UserName, @IU_Email, @IU_FirstName, @IU_LastName, @IU_Position, @IU_Department, @IU_RoleName;
END

CLOSE cur_internal;
DEALLOCATE cur_internal;

-- ============================================================
-- Section 3: External Users (with CompanyId)
-- Per company: 1 ExtAdmin + 2 ExtAppraisalStaff + 1 ExtAppraisalChecker + 1 ExtAppraisalVerifier
-- ============================================================

DECLARE @ExtUsers TABLE (
    CompanyName NVARCHAR(200),
    UserName NVARCHAR(256), Email NVARCHAR(256),
    FirstName NVARCHAR(100), LastName NVARCHAR(100),
    RoleName NVARCHAR(256)
);

INSERT INTO @ExtUsers VALUES
    -- Thai Appraisal Co., Ltd.
    (N'Thai Appraisal Co., Ltd.', N'th.admin1',    N'admin1@thai-appraisal.co.th',    N'Anucha',    N'Thammarat',  N'ExtAdmin'),
    (N'Thai Appraisal Co., Ltd.', N'th.staff1',    N'staff1@thai-appraisal.co.th',    N'Somchai',   N'Srisuk',     N'ExtAppraisalStaff'),
    (N'Thai Appraisal Co., Ltd.', N'th.staff2',    N'staff2@thai-appraisal.co.th',    N'Sureeporn', N'Thongdee',   N'ExtAppraisalStaff'),
    (N'Thai Appraisal Co., Ltd.', N'th.chk1',      N'checker1@thai-appraisal.co.th',  N'Pranee',    N'Petcharat',  N'ExtAppraisalChecker'),
    (N'Thai Appraisal Co., Ltd.', N'th.vrf1',      N'verifier1@thai-appraisal.co.th', N'Wichai',    N'Wongsawat',  N'ExtAppraisalVerifier'),

    -- Siam Valuation Group
    (N'Siam Valuation Group', N'sm.admin1',    N'admin1@siam-valuation.co.th',    N'Krisada',   N'Kittiwong',  N'ExtAdmin'),
    (N'Siam Valuation Group', N'sm.staff1',    N'staff1@siam-valuation.co.th',    N'Nattapong', N'Nakarin',    N'ExtAppraisalStaff'),
    (N'Siam Valuation Group', N'sm.staff2',    N'staff2@siam-valuation.co.th',    N'Apinya',    N'Aroonrat',   N'ExtAppraisalStaff'),
    (N'Siam Valuation Group', N'sm.chk1',      N'checker1@siam-valuation.co.th',  N'Kanokwan',  N'Kittisak',   N'ExtAppraisalChecker'),
    (N'Siam Valuation Group', N'sm.vrf1',      N'verifier1@siam-valuation.co.th', N'Thawatchai',N'Tangsiri',   N'ExtAppraisalVerifier'),

    -- Bangkok Property Appraisers
    (N'Bangkok Property Appraisers', N'bkk.admin1',    N'admin1@bkk-property.co.th',    N'Viroj',     N'Vorapong',   N'ExtAdmin'),
    (N'Bangkok Property Appraisers', N'bkk.staff1',    N'staff1@bkk-property.co.th',    N'Pornchai',  N'Prasert',    N'ExtAppraisalStaff'),
    (N'Bangkok Property Appraisers', N'bkk.staff2',    N'staff2@bkk-property.co.th',    N'Ratchanee', N'Rungruang',  N'ExtAppraisalStaff'),
    (N'Bangkok Property Appraisers', N'bkk.chk1',      N'checker1@bkk-property.co.th',  N'Siriporn',  N'Suwannarat', N'ExtAppraisalChecker'),
    (N'Bangkok Property Appraisers', N'bkk.vrf1',      N'verifier1@bkk-property.co.th', N'Arthit',    N'Anantachai', N'ExtAppraisalVerifier'),

    -- Eastern Appraisal Services
    (N'Eastern Appraisal Services', N'ea.admin1',    N'admin1@eastern-appraisal.co.th',    N'Somsak',   N'Sawaddee',   N'ExtAdmin'),
    (N'Eastern Appraisal Services', N'ea.staff1',    N'staff1@eastern-appraisal.co.th',    N'Chakrit',  N'Chaiyasit',  N'ExtAppraisalStaff'),
    (N'Eastern Appraisal Services', N'ea.staff2',    N'staff2@eastern-appraisal.co.th',    N'Duangjai', N'Decharat',   N'ExtAppraisalStaff'),
    (N'Eastern Appraisal Services', N'ea.chk1',      N'checker1@eastern-appraisal.co.th',  N'Ekachai',  N'Euajaroen',  N'ExtAppraisalChecker'),
    (N'Eastern Appraisal Services', N'ea.vrf1',      N'verifier1@eastern-appraisal.co.th', N'Fonthip',  N'Fuangfoo',   N'ExtAppraisalVerifier'),

    -- Northern Valuation Partners
    (N'Northern Valuation Partners', N'nth.admin1',    N'admin1@northern-val.co.th',    N'Preecha',   N'Phromma',    N'ExtAdmin'),
    (N'Northern Valuation Partners', N'nth.staff1',    N'staff1@northern-val.co.th',    N'Kittisak',  N'Kaewmanee',  N'ExtAppraisalStaff'),
    (N'Northern Valuation Partners', N'nth.staff2',    N'staff2@northern-val.co.th',    N'Supatra',   N'Saetang',    N'ExtAppraisalStaff'),
    (N'Northern Valuation Partners', N'nth.chk1',      N'checker1@northern-val.co.th',  N'Laddawan',  N'Lertpanich', N'ExtAppraisalChecker'),
    (N'Northern Valuation Partners', N'nth.vrf1',      N'verifier1@northern-val.co.th', N'Montri',    N'Meesuk',     N'ExtAppraisalVerifier'),

    -- Southern Property Consultants
    (N'Southern Property Consultants', N'sth.admin1',    N'admin1@southern-prop.co.th',    N'Surachai',  N'Srithong',   N'ExtAdmin'),
    (N'Southern Property Consultants', N'sth.staff1',    N'staff1@southern-prop.co.th',    N'Natthawut', N'Niyomtham',  N'ExtAppraisalStaff'),
    (N'Southern Property Consultants', N'sth.staff2',    N'staff2@southern-prop.co.th',    N'Waraporn',  N'Wisetsri',   N'ExtAppraisalStaff'),
    (N'Southern Property Consultants', N'sth.chk1',      N'checker1@southern-prop.co.th',  N'Orathai',   N'Ounjai',     N'ExtAppraisalChecker'),
    (N'Southern Property Consultants', N'sth.vrf1',      N'verifier1@southern-prop.co.th', N'Prawit',    N'Phanomwan',  N'ExtAppraisalVerifier');

-- Insert external users
DECLARE @EU_CompanyName NVARCHAR(200), @EU_UserName NVARCHAR(256), @EU_Email NVARCHAR(256),
        @EU_FirstName NVARCHAR(100), @EU_LastName NVARCHAR(100), @EU_RoleName NVARCHAR(256);
DECLARE @CompanyId UNIQUEIDENTIFIER;

DECLARE cur_external CURSOR LOCAL FAST_FORWARD FOR
    SELECT CompanyName, UserName, Email, FirstName, LastName, RoleName FROM @ExtUsers;

OPEN cur_external;
FETCH NEXT FROM cur_external INTO @EU_CompanyName, @EU_UserName, @EU_Email, @EU_FirstName, @EU_LastName, @EU_RoleName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @CompanyId = (SELECT Id FROM auth.Companies WHERE Name = @EU_CompanyName);

    IF @CompanyId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM auth.AspNetUsers WHERE NormalizedUserName = UPPER(@EU_UserName))
    BEGIN
        SET @SecurityStamp = UPPER(NEWID());
        SET @ConcurrencyStamp = CAST(NEWID() AS NVARCHAR(36));

        DECLARE @EU_UserId UNIQUEIDENTIFIER = NEWID();

        INSERT INTO auth.AspNetUsers (
            Id, UserName, NormalizedUserName, Email, NormalizedEmail,
            EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
            PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
            FirstName, LastName, Position, CompanyId,
            AvatarUrl
        ) VALUES (
            @EU_UserId, @EU_UserName, UPPER(@EU_UserName), @EU_Email, UPPER(@EU_Email),
            1, @PasswordHash, @SecurityStamp, @ConcurrencyStamp,
            0, 0, 1, 0,
            @EU_FirstName, @EU_LastName, @EU_RoleName, @CompanyId,
            N'https://ui-avatars.com/api/?name=' + @EU_FirstName + N'+' + @EU_LastName + N'&background=f59e0b&color=fff&size=128'
        );

        -- Assign role
        INSERT INTO auth.AspNetUserRoles (UserId, RoleId)
        SELECT @EU_UserId, r.Id
        FROM auth.AspNetRoles r
        WHERE r.NormalizedName = UPPER(@EU_RoleName);
    END

    FETCH NEXT FROM cur_external INTO @EU_CompanyName, @EU_UserName, @EU_Email, @EU_FirstName, @EU_LastName, @EU_RoleName;
END

CLOSE cur_external;
DEALLOCATE cur_external;

-- ============================================================
-- Section 4: Assign Admin role to the system admin user (created by C# seeder)
-- ============================================================

INSERT INTO auth.AspNetUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM auth.AspNetUsers u, auth.AspNetRoles r
WHERE u.NormalizedUserName = N'ADMIN' AND r.NormalizedName = N'ADMIN'
AND NOT EXISTS (
    SELECT 1 FROM auth.AspNetUserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id
);

PRINT 'Seed data for workflow users and roles completed.';

-- Seed workflow assignment groups from existing ASP.NET Identity roles.
-- Creates auth.Groups entries matching role names and copies user memberships
-- to auth.GroupUsers so the group-based assignment pipeline can resolve candidates.
--
-- Safe to run multiple times (idempotent via NOT EXISTS checks).

-- Step 1: Create groups matching workflow-relevant roles
INSERT INTO auth.Groups (Id, Name, Description, Scope, IsDeleted)
SELECT NEWID(), r.Name, 'Workflow assignment group: ' + r.Name, 'System', 0
FROM auth.AspNetRoles r
WHERE r.NormalizedName IN (
    'REQUESTCHECKER',
    'REQUESTMAKER',
    'ADMIN',
    'INTADMIN',
    'EXTADMIN',
    'EXTAPPRAISALSTAFF',
    'EXTAPPRAISALCHECKER',
    'EXTAPPRAISALVERIFIER',
    'INTAPPRAISALSTAFF',
    'INTAPPRAISALCHECKER',
    'INTAPPRAISALVERIFIER',
    'APPRAISALCOMMITTEE'
)
AND NOT EXISTS (
    SELECT 1 FROM auth.Groups g
    WHERE g.Name = r.Name AND g.IsDeleted = 0
);

-- Step 2: Copy user memberships from role assignments to group memberships
INSERT INTO auth.GroupUsers (GroupId, UserId)
SELECT g.Id, ur.UserId
FROM auth.AspNetUserRoles ur
INNER JOIN auth.AspNetRoles r ON r.Id = ur.RoleId
INNER JOIN auth.Groups g ON g.Name = r.Name AND g.IsDeleted = 0
WHERE r.NormalizedName IN (
    'REQUESTCHECKER',
    'REQUESTMAKER',
    'ADMIN',
    'INTADMIN',
    'EXTADMIN',
    'EXTAPPRAISALSTAFF',
    'EXTAPPRAISALCHECKER',
    'EXTAPPRAISALVERIFIER',
    'INTAPPRAISALSTAFF',
    'INTAPPRAISALCHECKER',
    'INTAPPRAISALVERIFIER',
    'APPRAISALCOMMITTEE'
)
AND NOT EXISTS (
    SELECT 1 FROM auth.GroupUsers gu
    WHERE gu.GroupId = g.Id AND gu.UserId = ur.UserId
);

-- Wortgleiche Entsprechung zur PostgreSQL-Fixture: dieselben Tabellen, dieselbe Hierarchie,
-- dieselben Daten. Nur so ist ein Zeilenvergleich zwischen den Providern aussagekraeftig.
IF OBJECT_ID('GlobalRolePermissions') IS NOT NULL DROP TABLE GlobalRolePermissions;
IF OBJECT_ID('GlobalToLocalRoles') IS NOT NULL DROP TABLE GlobalToLocalRoles;
IF OBJECT_ID('RolePermissions') IS NOT NULL DROP TABLE RolePermissions;
IF OBJECT_ID('Permissions') IS NOT NULL DROP TABLE Permissions;
IF OBJECT_ID('RoleRoles') IS NOT NULL DROP TABLE RoleRoles;
IF OBJECT_ID('TenantUserRoles') IS NOT NULL DROP TABLE TenantUserRoles;
IF OBJECT_ID('SecurityRoles') IS NOT NULL DROP TABLE SecurityRoles;
IF OBJECT_ID('TenantUsers') IS NOT NULL DROP TABLE TenantUsers;
IF OBJECT_ID('Users') IS NOT NULL DROP TABLE Users;
IF OBJECT_ID('Tenants') IS NOT NULL DROP TABLE Tenants;
GO

CREATE TABLE Tenants (
    TenantId int NOT NULL PRIMARY KEY,
    TenantName nvarchar(150) NOT NULL,
    DisplayName nvarchar(1024) NULL,
    TenantPassword nvarchar(125) NULL,
    TimeZone nvarchar(1024) NULL,
    TenantTypeId int NULL,
    TenantDirty bit NULL,
    ParentTenantId int NULL
);
GO

CREATE TABLE Users (
    Id nvarchar(450) NOT NULL PRIMARY KEY,
    NormalizedUserName nvarchar(256) NULL
);
GO

CREATE TABLE TenantUsers (
    TenantUserId int NOT NULL PRIMARY KEY,
    TenantId int NOT NULL,
    UserId nvarchar(450) NOT NULL
);
GO

CREATE TABLE SecurityRoles (
    RoleId int NOT NULL PRIMARY KEY,
    TenantId int NOT NULL,
    RoleName nvarchar(150) NULL
);
GO

CREATE TABLE TenantUserRoles (
    TenantUserId int NOT NULL,
    RoleId int NULL
);
GO

CREATE TABLE RoleRoles (
    PermissiveRoleId int NOT NULL,
    PermittedRoleId int NOT NULL
);
GO

CREATE TABLE Permissions (
    PermissionId int NOT NULL PRIMARY KEY,
    PermissionName nvarchar(150) NOT NULL
);
GO

CREATE TABLE RolePermissions (
    RoleId int NOT NULL,
    PermissionId int NOT NULL,
    TenantId int NULL
);
GO

CREATE TABLE GlobalToLocalRoles (
    GlobalRoleId int NOT NULL,
    LocalRoleId int NOT NULL
);
GO

CREATE TABLE GlobalRolePermissions (
    GlobalRoleId int NOT NULL,
    PermissionId int NOT NULL
);
GO

INSERT INTO Tenants (TenantId, TenantName, DisplayName, ParentTenantId) VALUES
    (1, 'T1', 'Wurzel', NULL),
    (2, 'T2', 'Mitte', 1),
    (3, 'T3', 'Blatt', 2);

INSERT INTO Users (Id, NormalizedUserName) VALUES ('alice', 'ALICE');

INSERT INTO TenantUsers (TenantUserId, TenantId, UserId) VALUES (1, 1, 'alice');

INSERT INTO SecurityRoles (RoleId, TenantId, RoleName) VALUES
    (1, 1, 'R1'), (2, 2, 'R2'), (3, 3, 'R3');

INSERT INTO TenantUserRoles (TenantUserId, RoleId) VALUES (1, 1);

INSERT INTO RoleRoles (PermissiveRoleId, PermittedRoleId) VALUES (2, 1), (3, 2);

INSERT INTO Permissions (PermissionId, PermissionName) VALUES
    (1, 'Perm.A'), (2, 'Perm.B'), (3, 'Perm.C');

INSERT INTO RolePermissions (RoleId, PermissionId, TenantId) VALUES
    (1, 1, 1), (3, 1, 3), (2, 2, 2);

INSERT INTO GlobalToLocalRoles (GlobalRoleId, LocalRoleId) VALUES (10, 2);
INSERT INTO GlobalRolePermissions (GlobalRoleId, PermissionId) VALUES (10, 3);
GO

-- Wortgleiche Entsprechung zu pgcheck/load_schema.sql, inklusive derselben Indizes.
IF OBJECT_ID('Widgets') IS NOT NULL DROP TABLE Widgets;
IF OBJECT_ID('DiagnosticsQueries') IS NOT NULL DROP TABLE DiagnosticsQueries;
IF OBJECT_ID('WebPlugins') IS NOT NULL DROP TABLE WebPlugins;
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
    TenantId int NOT NULL PRIMARY KEY, TenantName nvarchar(150) NOT NULL,
    DisplayName nvarchar(1024) NULL, TenantPassword nvarchar(125) NULL,
    TimeZone nvarchar(1024) NULL, TenantTypeId int NULL, TenantDirty bit NULL,
    ParentTenantId int NULL);
CREATE UNIQUE INDEX IX_UniqueTenant ON Tenants (TenantName);
CREATE INDEX IX_Tenants_ParentTenantId ON Tenants (ParentTenantId);
GO

CREATE TABLE Users (Id nvarchar(450) NOT NULL PRIMARY KEY, NormalizedUserName nvarchar(256) NULL);
GO

CREATE TABLE TenantUsers (TenantUserId int NOT NULL PRIMARY KEY, TenantId int NOT NULL, UserId nvarchar(450) NOT NULL);
CREATE INDEX IX_TenantUsers_TenantId ON TenantUsers (TenantId);
CREATE INDEX IX_TenantUsers_UserId ON TenantUsers (UserId);
GO

CREATE TABLE SecurityRoles (RoleId int NOT NULL PRIMARY KEY, TenantId int NOT NULL, RoleName nvarchar(150) NULL);
CREATE INDEX IX_SecurityRoles_TenantId ON SecurityRoles (TenantId);
GO

CREATE TABLE TenantUserRoles (TenantUserId int NOT NULL, RoleId int NULL);
CREATE INDEX IX_TenantUserRoles_TenantUserId ON TenantUserRoles (TenantUserId);
GO

CREATE TABLE RoleRoles (PermissiveRoleId int NOT NULL, PermittedRoleId int NOT NULL);
CREATE INDEX IX_RoleRoles_PermissiveRoleId ON RoleRoles (PermissiveRoleId);
CREATE INDEX IX_RoleRoles_PermittedRoleId ON RoleRoles (PermittedRoleId);
GO

CREATE TABLE Permissions (PermissionId int NOT NULL PRIMARY KEY, PermissionName nvarchar(150) NOT NULL);
GO

CREATE TABLE RolePermissions (RoleId int NOT NULL, PermissionId int NOT NULL, TenantId int NULL);
CREATE INDEX IX_RolePermissions_TenantId ON RolePermissions (TenantId);
CREATE INDEX IX_RolePermissions_PermissionId ON RolePermissions (PermissionId);
GO

CREATE TABLE GlobalToLocalRoles (GlobalRoleId int NOT NULL, LocalRoleId int NOT NULL);
CREATE TABLE GlobalRolePermissions (GlobalRoleId int NOT NULL, PermissionId int NOT NULL);
GO

CREATE TABLE WebPlugins (
    WebPluginId int NOT NULL PRIMARY KEY, UniqueName nvarchar(150) NOT NULL,
    TenantId int NULL, Inheritable bit NOT NULL DEFAULT 0);
CREATE INDEX IX_WebPlugins_TenantId ON WebPlugins (TenantId);
GO

CREATE TABLE DiagnosticsQueries (
    DiagnosticsQueryId int NOT NULL PRIMARY KEY, DiagnosticsQueryName nvarchar(150) NOT NULL, TenantId int NULL);
CREATE INDEX IX_DiagnosticsQueries_TenantId ON DiagnosticsQueries (TenantId);
GO

CREATE TABLE Widgets (
    DashboardWidgetId int NOT NULL PRIMARY KEY, SystemName nvarchar(150) NOT NULL, TenantId int NULL);
CREATE INDEX IX_Widgets_TenantId ON Widgets (TenantId);
GO

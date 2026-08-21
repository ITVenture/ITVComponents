-- Lastschema: dieselben Tabellen wie die Mini-Fixture, aber mit den INDIZES aus der echten
-- Initialmigration. Ohne die waeren Zeitmessungen wertlos - sie wuerden nur zeigen, wie schnell
-- PostgreSQL sequenziell liest.
DROP TABLE IF EXISTS "Widgets", "DiagnosticsQueries", "WebPlugins",
                     "GlobalRolePermissions", "GlobalToLocalRoles", "RolePermissions", "Permissions",
                     "RoleRoles", "TenantUserRoles", "SecurityRoles", "TenantUsers", "Users", "Tenants" CASCADE;

CREATE TABLE "Tenants" (
    "TenantId" integer PRIMARY KEY,
    "TenantName" character varying(150) NOT NULL,
    "DisplayName" character varying(1024),
    "TenantPassword" character varying(125),
    "TimeZone" character varying(1024),
    "TenantTypeId" integer,
    "TenantDirty" boolean,
    "ParentTenantId" integer
);
CREATE UNIQUE INDEX "IX_UniqueTenant" ON "Tenants" ("TenantName");
CREATE INDEX "IX_Tenants_ParentTenantId" ON "Tenants" ("ParentTenantId");

CREATE TABLE "Users" ("Id" text PRIMARY KEY, "NormalizedUserName" character varying(256));

CREATE TABLE "TenantUsers" (
    "TenantUserId" integer PRIMARY KEY, "TenantId" integer NOT NULL, "UserId" text NOT NULL);
CREATE INDEX "IX_TenantUsers_TenantId" ON "TenantUsers" ("TenantId");
CREATE INDEX "IX_TenantUsers_UserId" ON "TenantUsers" ("UserId");

CREATE TABLE "SecurityRoles" (
    "RoleId" integer PRIMARY KEY, "TenantId" integer NOT NULL, "RoleName" character varying(150));
CREATE INDEX "IX_SecurityRoles_TenantId" ON "SecurityRoles" ("TenantId");

CREATE TABLE "TenantUserRoles" ("TenantUserId" integer NOT NULL, "RoleId" integer);
CREATE INDEX "IX_TenantUserRoles_TenantUserId" ON "TenantUserRoles" ("TenantUserId");

CREATE TABLE "RoleRoles" ("PermissiveRoleId" integer NOT NULL, "PermittedRoleId" integer NOT NULL);
CREATE INDEX "IX_RoleRoles_PermissiveRoleId" ON "RoleRoles" ("PermissiveRoleId");
CREATE INDEX "IX_RoleRoles_PermittedRoleId" ON "RoleRoles" ("PermittedRoleId");

CREATE TABLE "Permissions" (
    "PermissionId" integer PRIMARY KEY, "PermissionName" character varying(150) NOT NULL);

CREATE TABLE "RolePermissions" (
    "RoleId" integer NOT NULL, "PermissionId" integer NOT NULL, "TenantId" integer);
CREATE INDEX "IX_RolePermissions_TenantId" ON "RolePermissions" ("TenantId");
CREATE INDEX "IX_RolePermissions_PermissionId" ON "RolePermissions" ("PermissionId");

CREATE TABLE "GlobalToLocalRoles" ("GlobalRoleId" integer NOT NULL, "LocalRoleId" integer NOT NULL);
CREATE TABLE "GlobalRolePermissions" ("GlobalRoleId" integer NOT NULL, "PermissionId" integer NOT NULL);

-- Plugins, Diagnose-Abfragen und Kacheln: die drei Bereiche, die ueber Vererbung erreichbar sein sollen.
CREATE TABLE "WebPlugins" (
    "WebPluginId" integer PRIMARY KEY, "UniqueName" character varying(150) NOT NULL,
    "TenantId" integer, "Inheritable" boolean NOT NULL DEFAULT false);
CREATE INDEX "IX_WebPlugins_TenantId" ON "WebPlugins" ("TenantId");

CREATE TABLE "DiagnosticsQueries" (
    "DiagnosticsQueryId" integer PRIMARY KEY, "DiagnosticsQueryName" character varying(150) NOT NULL,
    "TenantId" integer);
CREATE INDEX "IX_DiagnosticsQueries_TenantId" ON "DiagnosticsQueries" ("TenantId");

CREATE TABLE "Widgets" (
    "DashboardWidgetId" integer PRIMARY KEY, "SystemName" character varying(150) NOT NULL,
    "TenantId" integer);
CREATE INDEX "IX_Widgets_TenantId" ON "Widgets" ("TenantId");

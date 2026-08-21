-- Nur die Tabellen, die die Baum-Objekte anfassen, in der Schreibweise, die EF anlegt.
DROP TABLE IF EXISTS "GlobalRolePermissions", "GlobalToLocalRoles", "RolePermissions", "Permissions",
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

CREATE TABLE "Users" (
    "Id" text PRIMARY KEY,
    "NormalizedUserName" character varying(256)
);

CREATE TABLE "TenantUsers" (
    "TenantUserId" integer PRIMARY KEY,
    "TenantId" integer NOT NULL,
    "UserId" text NOT NULL
);

CREATE TABLE "SecurityRoles" (
    "RoleId" integer PRIMARY KEY,
    "TenantId" integer NOT NULL,
    "RoleName" character varying(150)
);

CREATE TABLE "TenantUserRoles" (
    "TenantUserId" integer NOT NULL,
    "RoleId" integer
);

CREATE TABLE "RoleRoles" (
    "PermissiveRoleId" integer NOT NULL,
    "PermittedRoleId" integer NOT NULL
);

CREATE TABLE "Permissions" (
    "PermissionId" integer PRIMARY KEY,
    "PermissionName" character varying(150) NOT NULL
);

CREATE TABLE "RolePermissions" (
    "RoleId" integer NOT NULL,
    "PermissionId" integer NOT NULL,
    "TenantId" integer
);

CREATE TABLE "GlobalToLocalRoles" (
    "GlobalRoleId" integer NOT NULL,
    "LocalRoleId" integer NOT NULL
);

CREATE TABLE "GlobalRolePermissions" (
    "GlobalRoleId" integer NOT NULL,
    "PermissionId" integer NOT NULL
);

-- Die Hierarchie: T1 -> T2 -> T3.
INSERT INTO "Tenants" ("TenantId","TenantName","DisplayName","ParentTenantId") VALUES
    (1,'T1','Wurzel',NULL),
    (2,'T2','Mitte',1),
    (3,'T3','Blatt',2);

INSERT INTO "Users" ("Id","NormalizedUserName") VALUES ('alice','ALICE');

-- alice haengt NUR am Wurzel-Mandanten. Alles Weitere muss der Baum liefern.
INSERT INTO "TenantUsers" ("TenantUserId","TenantId","UserId") VALUES (1,1,'alice');

INSERT INTO "SecurityRoles" ("RoleId","TenantId","RoleName") VALUES
    (1,1,'R1'), (2,2,'R2'), (3,3,'R3');

INSERT INTO "TenantUserRoles" ("TenantUserId","RoleId") VALUES (1,1);

-- "Permissive erreicht, wer Permitted haelt": R1 (oben) zieht R2 (Mitte) nach sich, R2 wiederum R3.
INSERT INTO "RoleRoles" ("PermissiveRoleId","PermittedRoleId") VALUES (2,1), (3,2);

INSERT INTO "Permissions" ("PermissionId","PermissionName") VALUES
    (1,'Perm.A'), (2,'Perm.B'), (3,'Perm.C');

-- Perm.A absichtlich NUR an R1 und R3, damit ein Treffer in T2 auffiele.
INSERT INTO "RolePermissions" ("RoleId","PermissionId","TenantId") VALUES
    (1,1,1), (3,1,3), (2,2,2);

-- Der Weg ueber eine globale Rolle: Perm.C haengt an einer globalen Rolle, die auf R2 zeigt.
INSERT INTO "GlobalToLocalRoles" ("GlobalRoleId","LocalRoleId") VALUES (10,2);
INSERT INTO "GlobalRolePermissions" ("GlobalRoleId","PermissionId") VALUES (10,3);

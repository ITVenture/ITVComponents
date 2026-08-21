-- 10 000 Mandanten. Aufbau bewusst zweiteilig:
--   1..100    eine KETTE - Mandant i haengt an i-1, also Tiefe 1 bis 100. Das ist die
--             Rekursionsgrenze, nicht ihre Naehe.
--   101..10000 ein BREITER Baum, dessen Eltern reihum die Kettenglieder 1..99 sind. Damit
--             liegt die tiefste Ebene bei 100, und es gibt auf jeder Ebene Geschwister.
INSERT INTO "Tenants" ("TenantId","TenantName","ParentTenantId")
SELECT i, 'T'||i,
       CASE WHEN i = 1 THEN NULL
            WHEN i <= 100 THEN i - 1
            ELSE ((i - 101) % 99) + 1 END
FROM generate_series(1, 10000) i;

-- Eine Rolle je Kettenglied, jede von der daruberliegenden erreichbar: 100 Stufen Rollen-Vererbung.
INSERT INTO "SecurityRoles" ("RoleId","TenantId","RoleName")
SELECT i, i, 'R'||i FROM generate_series(1, 100) i;

INSERT INTO "RoleRoles" ("PermissiveRoleId","PermittedRoleId")
SELECT i, i - 1 FROM generate_series(2, 100) i;

INSERT INTO "Users" ("Id","NormalizedUserName") VALUES ('alice','ALICE'), ('bob','BOB');

-- alice ganz oben, bob in der Mitte: einmal von der Wurzel aus rechnen, einmal von innen.
INSERT INTO "TenantUsers" ("TenantUserId","TenantId","UserId") VALUES (1, 1, 'alice'), (2, 50, 'bob');
INSERT INTO "TenantUserRoles" ("TenantUserId","RoleId") VALUES (1, 1), (2, 50);

INSERT INTO "Permissions" ("PermissionId","PermissionName")
SELECT i, 'Perm.'||i FROM generate_series(1, 20) i;

-- Jede Ketten-Rolle traegt eine Berechtigung; Perm.1 haengt oben, Perm.20 unten.
INSERT INTO "RolePermissions" ("RoleId","PermissionId","TenantId")
SELECT i, ((i - 1) % 20) + 1, i FROM generate_series(1, 100) i;

-- Plugins: ein globales, ein vererbbares an der Wurzel, ein NICHT vererbbares in der Mitte,
-- dazu Rauschen ueber den ganzen Baum.
INSERT INTO "WebPlugins" ("WebPluginId","UniqueName","TenantId","Inheritable") VALUES
    (1, 'DataSource', NULL, false),
    (2, 'DataSource', 1, true),
    (3, 'DataSource', 50, false);
INSERT INTO "WebPlugins" ("WebPluginId","UniqueName","TenantId","Inheritable")
SELECT 100 + i, 'Noise'||i, ((i - 1) % 10000) + 1, (i % 2 = 0)
FROM generate_series(1, 5000) i;

-- Diagnose-Abfragen und Kacheln: eine an der Wurzel (soll unten ankommen), eine global,
-- dazu Rauschen.
INSERT INTO "DiagnosticsQueries" ("DiagnosticsQueryId","DiagnosticsQueryName","TenantId") VALUES
    (1, 'QueryXYZ', 1),
    (2, 'GlobalQuery', NULL);
INSERT INTO "DiagnosticsQueries" ("DiagnosticsQueryId","DiagnosticsQueryName","TenantId")
SELECT 100 + i, 'Q'||i, ((i - 1) % 10000) + 1 FROM generate_series(1, 5000) i;

INSERT INTO "Widgets" ("DashboardWidgetId","SystemName","TenantId") VALUES
    (1, 'WidgetXYZ', 1),
    (2, 'GlobalWidget', NULL);
INSERT INTO "Widgets" ("DashboardWidgetId","SystemName","TenantId")
SELECT 100 + i, 'W'||i, ((i - 1) % 10000) + 1 FROM generate_series(1, 5000) i;

ANALYZE;

-- Wortgleiche Entsprechung zu pgcheck/load_data.sql: dieselben 10 000 Mandanten, dieselbe
-- zweiteilige Form (Kette 1..100 an der Rekursionsgrenze, breiter Baum 101..10000 darunter),
-- dieselben Rollen, Berechtigungen, Plugins, Diagnose-Abfragen und Kacheln.
SET NOCOUNT ON;

-- Zaehlerhilfe statt generate_series: sys.all_objects reicht doppelt gekreuzt weit ueber 10 000.
IF OBJECT_ID('tempdb..#n') IS NOT NULL DROP TABLE #n;
SELECT TOP (10000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
INTO #n
FROM sys.all_objects a CROSS JOIN sys.all_objects b;

INSERT INTO Tenants (TenantId, TenantName, ParentTenantId)
SELECT i, 'T' + CAST(i AS nvarchar(10)),
       CASE WHEN i = 1 THEN NULL
            WHEN i <= 100 THEN i - 1
            ELSE ((i - 101) % 99) + 1 END
FROM #n;

-- Eine Rolle je Kettenglied, jede von der darueberliegenden erreichbar: 100 Stufen Rollen-Vererbung.
INSERT INTO SecurityRoles (RoleId, TenantId, RoleName)
SELECT i, i, 'R' + CAST(i AS nvarchar(10)) FROM #n WHERE i <= 100;

INSERT INTO RoleRoles (PermissiveRoleId, PermittedRoleId)
SELECT i, i - 1 FROM #n WHERE i BETWEEN 2 AND 100;

INSERT INTO Users (Id, NormalizedUserName) VALUES ('alice','ALICE'), ('bob','BOB');

-- alice ganz oben, bob in der Mitte: einmal von der Wurzel aus rechnen, einmal von innen.
INSERT INTO TenantUsers (TenantUserId, TenantId, UserId) VALUES (1, 1, 'alice'), (2, 50, 'bob');
INSERT INTO TenantUserRoles (TenantUserId, RoleId) VALUES (1, 1), (2, 50);

INSERT INTO Permissions (PermissionId, PermissionName)
SELECT i, 'Perm.' + CAST(i AS nvarchar(10)) FROM #n WHERE i <= 20;

-- Jede Ketten-Rolle traegt eine Berechtigung; Perm.1 haengt oben, Perm.20 unten.
INSERT INTO RolePermissions (RoleId, PermissionId, TenantId)
SELECT i, ((i - 1) % 20) + 1, i FROM #n WHERE i <= 100;

-- Plugins: ein globales, ein vererbbares an der Wurzel, ein NICHT vererbbares in der Mitte,
-- dazu Rauschen ueber den ganzen Baum.
INSERT INTO WebPlugins (WebPluginId, UniqueName, TenantId, Inheritable) VALUES
    (1, 'DataSource', NULL, 0),
    (2, 'DataSource', 1, 1),
    (3, 'DataSource', 50, 0);
INSERT INTO WebPlugins (WebPluginId, UniqueName, TenantId, Inheritable)
SELECT 100 + i, 'Noise' + CAST(i AS nvarchar(10)), ((i - 1) % 10000) + 1,
       CASE WHEN i % 2 = 0 THEN 1 ELSE 0 END
FROM #n WHERE i <= 5000;

-- Diagnose-Abfragen und Kacheln: eine an der Wurzel (soll unten ankommen), eine global, dazu Rauschen.
INSERT INTO DiagnosticsQueries (DiagnosticsQueryId, DiagnosticsQueryName, TenantId) VALUES
    (1, 'QueryXYZ', 1),
    (2, 'GlobalQuery', NULL);
INSERT INTO DiagnosticsQueries (DiagnosticsQueryId, DiagnosticsQueryName, TenantId)
SELECT 100 + i, 'Q' + CAST(i AS nvarchar(10)), ((i - 1) % 10000) + 1 FROM #n WHERE i <= 5000;

INSERT INTO Widgets (DashboardWidgetId, SystemName, TenantId) VALUES
    (1, 'WidgetXYZ', 1),
    (2, 'GlobalWidget', NULL);
INSERT INTO Widgets (DashboardWidgetId, SystemName, TenantId)
SELECT 100 + i, 'W' + CAST(i AS nvarchar(10)), ((i - 1) % 10000) + 1 FROM #n WHERE i <= 5000;

DROP TABLE #n;

-- Gegenstueck zu ANALYZE: ohne aktuelle Statistiken waeren die Zeiten wertlos.
EXEC sp_updatestats;
GO

SELECT 'Tenants' AS objekt, count(*) AS anzahl FROM Tenants
UNION ALL SELECT 'UpwardsTenantTree', count(*) FROM UpwardsTenantTree
UNION ALL SELECT 'SecurityRoles', count(*) FROM SecurityRoles
UNION ALL SELECT 'WebPlugins', count(*) FROM WebPlugins
UNION ALL SELECT 'DiagnosticsQueries', count(*) FROM DiagnosticsQueries
UNION ALL SELECT 'Widgets', count(*) FROM Widgets;
GO

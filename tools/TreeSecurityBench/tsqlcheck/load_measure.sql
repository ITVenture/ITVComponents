-- Gegenstueck zu pgcheck/load_measure.sql: dieselben Messpunkte M0..M9, dieselbe Reihenfolge.
-- Gemessen wird mit SYSUTCDATETIME() um jede Anweisung; die Zahlen kommen am Ende als eine Tabelle.
SET NOCOUNT ON;
GO

IF OBJECT_ID('tempdb..#mess') IS NOT NULL DROP TABLE #mess;
CREATE TABLE #mess (nr int, punkt nvarchar(200), zeilen int, ms int);

DECLARE @t datetime2(3), @c int;

-- M0: Groesse des Baums (volle Materialisierung)
SET @t = SYSUTCDATETIME();
SELECT @c = count(*) FROM UpwardsTenantTree;
INSERT INTO #mess VALUES (0, 'Groesse des Baums (volle Materialisierung)', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

-- M1: CurrentTenantTree fuer das tiefste Kettenglied (T100)
SET @t = SYSUTCDATETIME();
SELECT @c = count(*) FROM (SELECT t.ParentTenantId FROM UpwardsTenantTree t
                           WHERE t.OutermostLeafTenantName = 'T100') x;
INSERT INTO #mess VALUES (1, 'CurrentTenantTree fuer T100 (Kettenende)', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

-- M2: CurrentTenantTree fuer ein Blatt im breiten Teil (T199, Tiefe 100)
SET @t = SYSUTCDATETIME();
SELECT @c = count(*) FROM (SELECT t.ParentTenantId FROM UpwardsTenantTree t
                           WHERE t.OutermostLeafTenantName = 'T199') x;
INSERT INTO #mess VALUES (2, 'CurrentTenantTree fuer T199 (breiter Teil)', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

-- M3: Diagnose-Abfragen, die von T199 aus sichtbar sind
SET @t = SYSUTCDATETIME();
SELECT @c = count(*) FROM DiagnosticsQueries q
WHERE q.TenantId IS NULL OR q.TenantId IN (
    SELECT t.ParentTenantId FROM UpwardsTenantTree t WHERE t.OutermostLeafTenantName = 'T199');
INSERT INTO #mess VALUES (3, 'Diagnose-Abfragen sichtbar von T199', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

-- M4: Kacheln, die von T199 aus sichtbar sind
SET @t = SYSUTCDATETIME();
SELECT @c = count(*) FROM Widgets w
WHERE w.TenantId IS NULL OR w.TenantId IN (
    SELECT t.ParentTenantId FROM UpwardsTenantTree t WHERE t.OutermostLeafTenantName = 'T199');
INSERT INTO #mess VALUES (4, 'Kacheln sichtbar von T199', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

-- M5: Plugin-Aufloesung DataSource von T199 aus (die drei Stufen des Selectors)
SET @t = SYSUTCDATETIME();
;WITH phase1 AS (
    SELECT pin.UniqueName, p.OutermostLeafTenantId, p.ParentLevel
    FROM UpwardsTenantTree p
    INNER JOIN WebPlugins pin ON p.ParentTenantId = pin.TenantId
    WHERE p.OutermostLeafTenantId = 199 AND (p.ParentLevel = 1 OR pin.Inheritable = 1)
), phase2 AS (
    SELECT g.OutermostLeafTenantId AS TenantId, g.UniqueName, min(g.ParentLevel) AS Level
    FROM phase1 g GROUP BY g.UniqueName, g.OutermostLeafTenantId
)
SELECT @c = count(*) FROM (
    SELECT pg.WebPluginId
    FROM phase2 p
    INNER JOIN UpwardsTenantTree t ON t.OutermostLeafTenantId = p.TenantId AND t.ParentLevel = p.Level
    INNER JOIN WebPlugins pg ON pg.UniqueName = p.UniqueName AND pg.TenantId = t.ParentTenantId
    WHERE pg.UniqueName = 'DataSource') y;
INSERT INTO #mess VALUES (5, 'Plugin-Aufloesung DataSource von T199', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

-- M6: Rollen-Baum nach oben, tiefstes Kettenglied
SET @t = SYSUTCDATETIME();
SELECT @c = count(*) FROM dbo.GetUpwardsRoleTreeForIdByLeafId('alice', 100);
INSERT INTO #mess VALUES (6, 'Rollen-Baum nach OBEN (T100)', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

-- M7: Rollen-Baum nach unten, von der Wurzel ueber alle 10000.
-- Die Prozedur macht intern KEIN INSERT ... EXEC, also laesst sie sich hier abgreifen - damit
-- bleibt die Messung serverseitig und die Zeilen wandern nicht ueber die Leitung.
IF OBJECT_ID('tempdb..#dtree') IS NOT NULL DROP TABLE #dtree;
CREATE TABLE #dtree (ViewpointTenantId int, ViewpointTenantName nvarchar(100), TopmostTenantId int,
                     TopmostTenantName nvarchar(100), ChildTenantId int, ChildTenantName nvarchar(100),
                     TenantUserId int, UserId nvarchar(100), ResultingChildRoleId int, ChildLevel int,
                     TopmostParentLevel int);
SET @t = SYSUTCDATETIME();
INSERT INTO #dtree EXEC GetDownwardsRoleTreeProc @pUserId = 'alice', @puserIsLabels = 0, @pViewPoint = 'T1';
SET @c = @@ROWCOUNT;
INSERT INTO #mess VALUES (7, 'Rollen-Baum nach UNTEN von der Wurzel', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

-- M8/M9: GetChildTenantsWithPermsProc macht intern selbst ein INSERT ... EXEC; T-SQL verbietet die
-- Schachtelung, deshalb geht das Ergebnis hier an den Aufrufer. Die Zeit enthaelt damit auch das
-- Uebertragen von hoechstens 10 000 Zeilen - beim Vergleich mit PostgreSQL mitdenken.
SET @t = SYSUTCDATETIME();
EXEC GetChildTenantsWithPermsProc @UserId = 'alice', @UserIsLabels = 0,
     @ViewPointTenantName = 'T1', @RequiredPermissionArray = '["Perm.5"]';
SET @c = @@ROWCOUNT;
INSERT INTO #mess VALUES (8, 'Kind-Mandanten mit Berechtigung, von der Wurzel', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

SET @t = SYSUTCDATETIME();
EXEC GetChildTenantsWithPermsProc @UserId = 'bob', @UserIsLabels = 0,
     @ViewPointTenantName = 'T50', @RequiredPermissionArray = '["Perm.5"]';
SET @c = @@ROWCOUNT;
INSERT INTO #mess VALUES (9, 'Kind-Mandanten mit Berechtigung, aus der Mitte (bob/T50)', @c, DATEDIFF(ms, @t, SYSUTCDATETIME()));

DROP TABLE #dtree;
GO

PRINT '#ERGEBNIS';
SELECT nr, punkt, zeilen, ms FROM #mess ORDER BY nr;
GO

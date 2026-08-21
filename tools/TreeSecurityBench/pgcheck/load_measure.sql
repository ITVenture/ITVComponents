\timing on
SET statement_timeout = '1800s';

\echo '--- M0: Groesse des Baums (volle Materialisierung) ---'
SELECT count(*) AS zeilen FROM "UpwardsTenantTree";

\echo '--- M1: CurrentTenantTree fuer das tiefste Kettenglied (T100) ---'
SELECT count(*) FROM (
  SELECT t."ParentTenantId" FROM "UpwardsTenantTree" t
  WHERE t."OutermostLeafTenantName" = 'T100' ORDER BY t."ParentLevel") x;

\echo '--- M2: CurrentTenantTree fuer ein Blatt im breiten Teil (T199, Tiefe 100) ---'
SELECT count(*) FROM (
  SELECT t."ParentTenantId" FROM "UpwardsTenantTree" t
  WHERE t."OutermostLeafTenantName" = 'T199' ORDER BY t."ParentLevel") x;

\echo '--- M3: Diagnose-Abfragen, die von T199 aus sichtbar sind ---'
SELECT count(*) FROM "DiagnosticsQueries" q
WHERE q."TenantId" IS NULL OR q."TenantId" IN (
  SELECT t."ParentTenantId" FROM "UpwardsTenantTree" t WHERE t."OutermostLeafTenantName" = 'T199');

\echo '--- M4: Kacheln, die von T199 aus sichtbar sind ---'
SELECT count(*) FROM "Widgets" w
WHERE w."TenantId" IS NULL OR w."TenantId" IN (
  SELECT t."ParentTenantId" FROM "UpwardsTenantTree" t WHERE t."OutermostLeafTenantName" = 'T199');

\echo '--- M5: Plugin-Aufloesung DataSource von T199 aus (die drei Stufen des Selectors) ---'
WITH phase1 AS (
    SELECT pin."UniqueName", p."OutermostLeafTenantId", p."ParentLevel"
    FROM "UpwardsTenantTree" p
    INNER JOIN "WebPlugins" pin ON p."ParentTenantId" = pin."TenantId"
    WHERE p."OutermostLeafTenantId" = 199 AND (p."ParentLevel" = 1 OR pin."Inheritable")
), phase2 AS (
    SELECT g."OutermostLeafTenantId" AS "TenantId", g."UniqueName", min(g."ParentLevel") AS "Level"
    FROM phase1 g GROUP BY g."UniqueName", g."OutermostLeafTenantId"
)
SELECT count(*) FROM (
    SELECT pg.*
    FROM phase2 p
    INNER JOIN "UpwardsTenantTree" t ON t."OutermostLeafTenantId" = p."TenantId" AND t."ParentLevel" = p."Level"
    INNER JOIN "WebPlugins" pg ON pg."UniqueName" = p."UniqueName" AND pg."TenantId" = t."ParentTenantId"
    WHERE pg."UniqueName" = 'DataSource') y;

\echo '--- M6: Rollen-Baum nach oben, tiefstes Kettenglied ---'
SELECT count(*) FROM "GetUpwardsRoleTreeForIdByLeafId"('alice', 100);

\echo '--- M7: Rollen-Baum nach unten, von der Wurzel ueber alle 10000 ---'
SELECT count(*) FROM "GetDownwardsRoleTreeProc"('alice', false, 'T1');

\echo '--- M8: Kind-Mandanten mit einer Berechtigung, von der Wurzel ---'
SELECT count(*) FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.5"]');

\echo '--- M9: dasselbe aus der Mitte (bob in T50) ---'
SELECT count(*) FROM "GetChildTenantsWithPermsProc"('bob', false, 'T50', '["Perm.5"]');

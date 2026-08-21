-- Der Waechter sitzt jetzt in der Funktion unter der Sicht, nicht mehr in der Sicht selbst.
-- Also nochmal nachweisen, dass ein Mandanten-Zyklus weiterhin LAUT scheitert und nicht still
-- ein Teilergebnis liefert.
\set ON_ERROR_STOP off

DELETE FROM "TenantUserRoles";
DELETE FROM "TenantUsers";
DELETE FROM "SecurityRoles";
DELETE FROM "RoleRoles";
DELETE FROM "Tenants";

-- T1 -> T2 -> T3 -> T1: ein Ring.
INSERT INTO "Tenants" ("TenantId","TenantName","ParentTenantId") VALUES
    (1,'T1',3), (2,'T2',1), (3,'T3',2);

\echo '--- Zyklus ueber die Sicht (muss werfen, SQLSTATE 54001) ---'
SELECT count(*) FROM "UpwardsTenantTree";

\echo '--- Zyklus ueber die Funktion direkt (muss ebenfalls werfen) ---'
SELECT count(*) FROM "GetUpwardsTenantTreeByLeafId"(1);

\echo '--- und der gefilterte Weg, der jetzt der Normalfall ist ---'
SELECT count(*) FROM "UpwardsTenantTree" WHERE "OutermostLeafTenantName" = 'T2';

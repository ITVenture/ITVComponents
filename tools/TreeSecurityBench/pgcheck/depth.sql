-- Wie tief darf die Kette wirklich sein? Nicht hergeleitet, sondern gemessen.
-- Fuer jede Laenge: Mandanten neu aufbauen, den Baum nach oben rechnen, Ergebnis oder Fehler melden.
\set ON_ERROR_STOP off

CREATE OR REPLACE FUNCTION pg_temp.probe(p_len integer) RETURNS text
LANGUAGE plpgsql AS $$
DECLARE
    n integer;
BEGIN
    DELETE FROM "TenantUserRoles";
    DELETE FROM "TenantUsers";
    DELETE FROM "SecurityRoles";
    DELETE FROM "RoleRoles";
    DELETE FROM "Tenants";
    INSERT INTO "Tenants" ("TenantId","TenantName","ParentTenantId")
    SELECT i, 'T'||i, CASE WHEN i = 1 THEN NULL ELSE i - 1 END
    FROM generate_series(1, p_len) i;

    SELECT max("ParentLevel") INTO n FROM "UpwardsTenantTree";
    RETURN 'OK, tiefste Ebene ' || n;
EXCEPTION WHEN OTHERS THEN
    RETURN 'ABBRUCH: ' || SQLERRM;
END;
$$;

SELECT laenge, pg_temp.probe(laenge) AS ergebnis
FROM (VALUES (98),(99),(100),(101),(102)) v(laenge);

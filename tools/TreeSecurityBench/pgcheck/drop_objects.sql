-- Raeumt Sichten und Funktionen weg, damit objects.sql (reine CREATEs) frisch aufgelegt werden kann.
-- Tabellen und Daten bleiben unangetastet.
DO $$
DECLARE r record;
BEGIN
    FOR r IN SELECT table_name FROM information_schema.views WHERE table_schema = 'public' LOOP
        EXECUTE format('DROP VIEW IF EXISTS %I CASCADE', r.table_name);
    END LOOP;
    FOR r IN SELECT p.oid::regprocedure AS sig FROM pg_proc p
             JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'public' LOOP
        EXECUTE 'DROP FUNCTION ' || r.sig || ' CASCADE';
    END LOOP;
END $$;

SELECT (SELECT count(*) FROM information_schema.views WHERE table_schema = 'public') AS sichten,
       (SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'public') AS funktionen;

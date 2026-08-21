SELECT 'Tenants' AS objekt, count(*) FROM "Tenants"
UNION ALL SELECT 'UpwardsTenantTree', count(*) FROM "UpwardsTenantTree"
UNION ALL SELECT 'SecurityRoles', count(*) FROM "SecurityRoles"
UNION ALL SELECT 'TenantUsers', count(*) FROM "TenantUsers"
UNION ALL SELECT 'WebPlugins', count(*) FROM "WebPlugins"
UNION ALL SELECT 'DiagnosticsQueries', count(*) FROM "DiagnosticsQueries"
UNION ALL SELECT 'Widgets', count(*) FROM "Widgets";
SELECT count(*) AS funktionen FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'public';

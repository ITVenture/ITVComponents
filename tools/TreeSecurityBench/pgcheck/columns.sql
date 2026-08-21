-- Was EF sieht: die Spalten der Views und der Funktionen, verglichen mit den Eigenschaften
-- der Modellklassen. Auf PostgreSQL entscheidet hier die Schreibweise.
CREATE TEMP VIEW "vUpRole" AS SELECT * FROM "GetUpwardsRoleTreeForId"('alice', NULL::text);
CREATE TEMP VIEW "vDownRole" AS SELECT * FROM "GetDownwardsRoleTreeProc"('alice', false, 'T1');
CREATE TEMP VIEW "vChildTenants" AS SELECT * FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.A"]');

WITH expected(objekt, spalte) AS (
    VALUES
    -- UpwardsTenantView
    ('UpwardsTenantTree','OutermostLeafTenantId'), ('UpwardsTenantTree','OutermostLeafTenantName'),
    ('UpwardsTenantTree','ParentTenantId'), ('UpwardsTenantTree','ParentTenantName'),
    ('UpwardsTenantTree','ParentLevel'),
    -- DownwardsTenantView
    ('DownwardsTenantTree','TopmostTenantId'), ('DownwardsTenantTree','TopmostTenantName'),
    ('DownwardsTenantTree','ChildTenantId'), ('DownwardsTenantTree','ChildTenantName'),
    ('DownwardsTenantTree','ChildLevel'),
    -- UserAccessTree<string>
    ('TenantAccessTree','OutermostLeafTenantId'), ('TenantAccessTree','OutermostLeafTenantName'),
    ('TenantAccessTree','ParentTenantId'), ('TenantAccessTree','ParentTenantName'),
    ('TenantAccessTree','ParentLevel'), ('TenantAccessTree','TopmostTenantId'),
    ('TenantAccessTree','TopmostTenantName'), ('TenantAccessTree','ChildTenantId'),
    ('TenantAccessTree','ChildTenantName'), ('TenantAccessTree','TenantUserId'),
    ('TenantAccessTree','UserId'), ('TenantAccessTree','DirectAssign'),
    -- UpwardsRoleUserView<string>
    ('vUpRole','UserId'), ('vUpRole','ParentTenantId'), ('vUpRole','ParentTenantName'),
    ('vUpRole','TenantUserId'), ('vUpRole','OutermostLeafTenantId'), ('vUpRole','OutermostLeafTenantName'),
    ('vUpRole','ParentLevel'), ('vUpRole','OutermostRoleId'),
    -- DownwardsUserRoleView<string>
    ('vDownRole','UserId'), ('vDownRole','TopmostTenantId'), ('vDownRole','TopmostTenantName'),
    ('vDownRole','TopmostParentLevel'), ('vDownRole','ViewPointTenantId'), ('vDownRole','ViewpointTenantName'),
    ('vDownRole','ChildTenantId'), ('vDownRole','ChildTenantName'), ('vDownRole','ChildLevel'),
    ('vDownRole','TenantUserId'), ('vDownRole','ResultingChildRoleId'),
    -- HierarchyTenant
    ('vChildTenants','TenantId'), ('vChildTenants','ParentTenantId'), ('vChildTenants','TenantName'),
    ('vChildTenants','DisplayName'), ('vChildTenants','TenantPassword'), ('vChildTenants','TimeZone'),
    ('vChildTenants','TenantTypeId'), ('vChildTenants','TenantDirty')
)
SELECT e.objekt, e.spalte,
       CASE WHEN a.column_name IS NULL THEN '>>> FEHLT ODER ANDERS GESCHRIEBEN' ELSE 'OK' END AS ergebnis
FROM expected e
LEFT JOIN information_schema.columns a
       ON a.table_name = e.objekt AND a.column_name = e.spalte
WHERE a.column_name IS NULL
ORDER BY e.objekt, e.spalte;

SELECT 'Fehlende Spalten insgesamt: ' || count(*)::text AS zusammenfassung
FROM (VALUES
    ('UpwardsTenantTree','OutermostLeafTenantId'), ('UpwardsTenantTree','OutermostLeafTenantName'),
    ('UpwardsTenantTree','ParentTenantId'), ('UpwardsTenantTree','ParentTenantName'),
    ('UpwardsTenantTree','ParentLevel'),
    ('DownwardsTenantTree','TopmostTenantId'), ('DownwardsTenantTree','TopmostTenantName'),
    ('DownwardsTenantTree','ChildTenantId'), ('DownwardsTenantTree','ChildTenantName'),
    ('DownwardsTenantTree','ChildLevel'),
    ('TenantAccessTree','OutermostLeafTenantId'), ('TenantAccessTree','OutermostLeafTenantName'),
    ('TenantAccessTree','ParentTenantId'), ('TenantAccessTree','ParentTenantName'),
    ('TenantAccessTree','ParentLevel'), ('TenantAccessTree','TopmostTenantId'),
    ('TenantAccessTree','TopmostTenantName'), ('TenantAccessTree','ChildTenantId'),
    ('TenantAccessTree','ChildTenantName'), ('TenantAccessTree','TenantUserId'),
    ('TenantAccessTree','UserId'), ('TenantAccessTree','DirectAssign'),
    ('vUpRole','UserId'), ('vUpRole','ParentTenantId'), ('vUpRole','ParentTenantName'),
    ('vUpRole','TenantUserId'), ('vUpRole','OutermostLeafTenantId'), ('vUpRole','OutermostLeafTenantName'),
    ('vUpRole','ParentLevel'), ('vUpRole','OutermostRoleId'),
    ('vDownRole','UserId'), ('vDownRole','TopmostTenantId'), ('vDownRole','TopmostTenantName'),
    ('vDownRole','TopmostParentLevel'), ('vDownRole','ViewPointTenantId'), ('vDownRole','ViewpointTenantName'),
    ('vDownRole','ChildTenantId'), ('vDownRole','ChildTenantName'), ('vDownRole','ChildLevel'),
    ('vDownRole','TenantUserId'), ('vDownRole','ResultingChildRoleId'),
    ('vChildTenants','TenantId'), ('vChildTenants','ParentTenantId'), ('vChildTenants','TenantName'),
    ('vChildTenants','DisplayName'), ('vChildTenants','TenantPassword'), ('vChildTenants','TimeZone'),
    ('vChildTenants','TenantTypeId'), ('vChildTenants','TenantDirty')
) e(objekt, spalte)
LEFT JOIN information_schema.columns a ON a.table_name = e.objekt AND a.column_name = e.spalte
WHERE a.column_name IS NULL;

-- Erwartungswerte von Hand aus der T-SQL-Vorlage hergeleitet, nicht aus dem Lauf abgelesen.
WITH checks(nr, name, actual, expected) AS (
    VALUES
    (1, 'UpwardsTenantTree: Zeilen',
        (SELECT count(*)::text FROM "UpwardsTenantTree"), '6'),
    (2, 'UpwardsTenantTree: T3 klettert bis zur Wurzel',
        (SELECT string_agg("ParentTenantId"::text, ',' ORDER BY "ParentLevel")
         FROM "UpwardsTenantTree" WHERE "OutermostLeafTenantId" = 3), '3,2,1'),
    (3, 'DownwardsTenantTree: Zeilen',
        (SELECT count(*)::text FROM "DownwardsTenantTree"), '6'),
    (4, 'DownwardsTenantTree: T1 erreicht alle',
        (SELECT string_agg("ChildTenantId"::text, ',' ORDER BY "ChildLevel")
         FROM "DownwardsTenantTree" WHERE "TopmostTenantId" = 1), '1,2,3'),
    (5, 'TenantAccessTreeDown: Zeilen',
        (SELECT count(*)::text FROM "TenantAccessTreeDown"), '3'),
    (6, 'TenantAccessTreeDown: erreichte Mandanten',
        (SELECT string_agg(DISTINCT "ChildTenantId"::text, ',' ORDER BY "ChildTenantId"::text)
         FROM "TenantAccessTreeDown"), '1,2,3'),
    (7, 'TenantAccessTreeUp: liefert Zeilen',
        (SELECT (count(*) > 0)::text FROM "TenantAccessTreeUp"), 'true'),
    (8, 'TenantAccessTree: Zeilen',
        (SELECT count(*)::text FROM "TenantAccessTree"), '6'),
    (9, 'TenantAccessTree: direkt zugewiesen',
        (SELECT count(*)::text FROM "TenantAccessTree" WHERE "DirectAssign"), '3'),
    (10, 'GetEffectiveTenantUserRoles(1): bleibt im eigenen Mandanten',
        (SELECT string_agg("RoleId"::text, ',' ORDER BY "RoleId")
         FROM "GetEffectiveTenantUserRoles"(1)), '1'),
    (11, 'GetUpwardsRoleTreeForId(alice): erreichte Blaetter',
        (SELECT string_agg(DISTINCT "OutermostLeafTenantId"::text, ',' ORDER BY "OutermostLeafTenantId"::text)
         FROM "GetUpwardsRoleTreeForId"('alice', NULL::text)), '1,2,3'),
    (12, 'GetUpwardsRoleTreeForId(alice): Rolle je Blatt',
        (SELECT string_agg("OutermostRoleId"::text, ',' ORDER BY "OutermostLeafTenantId")
         FROM "GetUpwardsRoleTreeForId"('alice', NULL::text)), '1,2,3'),
    (13, 'GetUpwardsRoleTreeForId(alice): Ebene je Blatt',
        (SELECT string_agg("ParentLevel"::text, ',' ORDER BY "OutermostLeafTenantId")
         FROM "GetUpwardsRoleTreeForId"('alice', NULL::text)), '1,2,3'),
    (14, 'GetUpwardsRoleTreeForId(alice, T1): auf ein Blatt eingeschraenkt',
        (SELECT count(*)::text FROM "GetUpwardsRoleTreeForId"('alice', 'T1')), '1'),
    (15, 'GetUpwardsRoleTreeForLabels: gleich wie ueber die Id',
        (SELECT string_agg(DISTINCT "OutermostLeafTenantId"::text, ',' ORDER BY "OutermostLeafTenantId"::text)
         FROM "GetUpwardsRoleTreeForLabels"('["ALICE"]', NULL::text)), '1,2,3'),
    (16, 'GetUpwardsRoleTreeForLabels: unbekanntes Kennzeichen liefert nichts',
        (SELECT count(*)::text FROM "GetUpwardsRoleTreeForLabels"('["NIEMAND"]', NULL::text)), '0'),
    (17, 'GetUpwardsRoleTreeForIdByLeafId(alice, 3)',
        (SELECT count(*)::text FROM "GetUpwardsRoleTreeForIdByLeafId"('alice', 3)), '1'),
    (18, 'GetUpwardsRoleTreeForLabelsByLeafId(ALICE, 3)',
        (SELECT count(*)::text FROM "GetUpwardsRoleTreeForLabelsByLeafId"('["ALICE"]', 3)), '1'),
    (19, 'GetDownwardsRoleTreeProc(alice, T1): erreichte Mandanten',
        (SELECT string_agg("ChildTenantId"::text, ',' ORDER BY "ChildTenantId")
         FROM "GetDownwardsRoleTreeProc"('alice', false, 'T1')), '1,2,3'),
    (20, 'GetDownwardsRoleTreeProc(alice, T1): Rolle je Mandant',
        (SELECT string_agg("ResultingChildRoleId"::text, ',' ORDER BY "ChildTenantId")
         FROM "GetDownwardsRoleTreeProc"('alice', false, 'T1')), '1,2,3'),
    (21, 'GetDownwardsRoleTreeProc: Blickwinkel wird durchgereicht',
        (SELECT string_agg(DISTINCT "ViewPointTenantId"::text, ',' ORDER BY "ViewPointTenantId"::text)
         FROM "GetDownwardsRoleTreeProc"('alice', false, 'T1')), '1'),
    (22, 'GetDownwardsRoleTreeProc ueber Kennzeichen: gleiches Ergebnis',
        (SELECT string_agg("ChildTenantId"::text, ',' ORDER BY "ChildTenantId")
         FROM "GetDownwardsRoleTreeProc"('["ALICE"]', true, 'T1')), '1,2,3'),
    (23, 'GetDownwardsRoleTreeByVpIdProc(alice, 1)',
        (SELECT string_agg("ChildTenantId"::text, ',' ORDER BY "ChildTenantId")
         FROM "GetDownwardsRoleTreeByVpIdProc"('alice', false, 1)), '1,2,3'),
    (24, 'GetChildTenantsWithPermsProc: Perm.A haengt nur an R1 und R3',
        (SELECT string_agg("TenantId"::text, ',' ORDER BY "TenantId")
         FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.A"]')), '1,3'),
    (25, 'GetChildTenantsWithPermsProc: Perm.B nur an R2',
        (SELECT string_agg("TenantId"::text, ',' ORDER BY "TenantId")
         FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.B"]')), '2'),
    (26, 'GetChildTenantsWithPermsProc: Perm.C ueber die globale Rolle',
        (SELECT string_agg("TenantId"::text, ',' ORDER BY "TenantId")
         FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.C"]')), '2'),
    (27, 'GetChildTenantsWithPermsProc: unbekannte Berechtigung liefert nichts',
        (SELECT count(*)::text FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.X"]')), '0'),
    (28, 'GetChildTenantsWithPermsProc: TenantPassword bleibt leer',
        (SELECT count(*)::text FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.A"]')
         WHERE "TenantPassword" IS NOT NULL), '0'),
    (29, 'GetChildTenantsWithPermsProc: Elternbezug kommt mit',
        (SELECT string_agg(coalesce("ParentTenantId"::text,'-'), ',' ORDER BY "TenantId")
         FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.A"]')), '-,2'),
    (30, 'GetChildTenantsWithPermsProc ueber Kennzeichen',
        (SELECT string_agg("TenantId"::text, ',' ORDER BY "TenantId")
         FROM "GetChildTenantsWithPermsProc"('["ALICE"]', true, 'T1', '["Perm.A"]')), '1,3'),
    (31, 'GetChildTenantsWithPermsByVpIdProc(alice, 1)',
        (SELECT string_agg("TenantId"::text, ',' ORDER BY "TenantId")
         FROM "GetChildTenantsWithPermsByVpIdProc"('alice', false, 1, '["Perm.A"]')), '1,3'),
    (32, 'GetChildTenantsWithPermsByVpIdProc ueber Kennzeichen',
        (SELECT string_agg("TenantId"::text, ',' ORDER BY "TenantId")
         FROM "GetChildTenantsWithPermsByVpIdProc"('["ALICE"]', true, 1, '["Perm.A"]')), '1,3')
)
SELECT nr,
       name,
       coalesce(actual, '(nichts)') AS geliefert,
       expected AS erwartet,
       CASE WHEN actual IS NOT DISTINCT FROM expected THEN 'OK' ELSE '>>> ABWEICHUNG' END AS ergebnis
FROM checks
ORDER BY nr;

-- Zeilenweiser Abzug aller Baum-Objekte, in einer Form, die sich mit der SQL-Server-Seite
-- Zeichen fuer Zeichen vergleichen laesst. Nullwerte werden zu '-', damit beide Datenbanken
-- dasselbe schreiben statt "leer" gegen "NULL".
SELECT 'UPT|' || "OutermostLeafTenantId" || '|' || "OutermostLeafTenantName" || '|' ||
       "ParentTenantId" || '|' || "ParentTenantName" || '|' || "ParentLevel"
FROM "UpwardsTenantTree"
ORDER BY "OutermostLeafTenantId", "ParentTenantId", "ParentLevel";

SELECT 'DNT|' || "TopmostTenantId" || '|' || "TopmostTenantName" || '|' ||
       "ChildTenantId" || '|' || "ChildTenantName" || '|' || "ChildLevel"
FROM "DownwardsTenantTree"
ORDER BY "TopmostTenantId", "ChildTenantId", "ChildLevel";

SELECT 'TAT|' || "OutermostLeafTenantId" || '|' || "OutermostLeafTenantName" || '|' ||
       "ParentTenantId" || '|' || "ParentTenantName" || '|' || "ParentLevel" || '|' ||
       "TopmostTenantId" || '|' || "TopmostTenantName" || '|' ||
       "ChildTenantId" || '|' || "ChildTenantName" || '|' ||
       "TenantUserId" || '|' || "UserId" || '|' || (CASE WHEN "DirectAssign" THEN 1 ELSE 0 END)
FROM "TenantAccessTree"
ORDER BY "OutermostLeafTenantId", "ParentTenantId", "ChildTenantId", "TenantUserId";

SELECT 'EFF|' || "RoleId"
FROM "GetEffectiveTenantUserRoles"(1)
ORDER BY "RoleId";

SELECT 'URI|' || "OutermostLeafTenantId" || '|' || "OutermostLeafTenantName" || '|' ||
       "ParentTenantId" || '|' || "ParentTenantName" || '|' || "ParentRoleId" || '|' ||
       "ParentLevel" || '|' || "TenantUserId" || '|' || "UserId" || '|' || "OutermostRoleId"
FROM "GetUpwardsRoleTreeForId"('alice', NULL::text)
ORDER BY "OutermostLeafTenantId", "ParentTenantId", "ParentLevel", "OutermostRoleId";

SELECT 'URL|' || "OutermostLeafTenantId" || '|' || "OutermostLeafTenantName" || '|' ||
       "ParentTenantId" || '|' || "ParentTenantName" || '|' || "ParentRoleId" || '|' ||
       "ParentLevel" || '|' || "TenantUserId" || '|' || "UserId" || '|' || "OutermostRoleId"
FROM "GetUpwardsRoleTreeForLabels"('["ALICE"]', NULL::text)
ORDER BY "OutermostLeafTenantId", "ParentTenantId", "ParentLevel", "OutermostRoleId";

SELECT 'URB|' || "OutermostLeafTenantId" || '|' || "ParentTenantId" || '|' ||
       "ParentRoleId" || '|' || "ParentLevel" || '|' || "OutermostRoleId"
FROM "GetUpwardsRoleTreeForIdByLeafId"('alice', 3)
ORDER BY "OutermostLeafTenantId", "ParentTenantId", "ParentLevel", "OutermostRoleId";

SELECT 'DRT|' || "ViewPointTenantId" || '|' || "ViewpointTenantName" || '|' ||
       "TopmostTenantId" || '|' || "TopmostTenantName" || '|' ||
       "ChildTenantId" || '|' || "ChildTenantName" || '|' ||
       "TenantUserId" || '|' || "UserId" || '|' || "ResultingChildRoleId" || '|' ||
       "ChildLevel" || '|' || "TopmostParentLevel"
FROM "GetDownwardsRoleTreeProc"('alice', false, 'T1')
ORDER BY "ChildTenantId", "ResultingChildRoleId", "TopmostTenantId";

SELECT 'DRL|' || "ViewPointTenantId" || '|' || "ChildTenantId" || '|' ||
       "ResultingChildRoleId" || '|' || "ChildLevel" || '|' || "TopmostParentLevel"
FROM "GetDownwardsRoleTreeProc"('["ALICE"]', true, 'T1')
ORDER BY "ChildTenantId", "ResultingChildRoleId";

SELECT 'DRV|' || "ViewPointTenantId" || '|' || "ChildTenantId" || '|' ||
       "ResultingChildRoleId" || '|' || "ChildLevel" || '|' || "TopmostParentLevel"
FROM "GetDownwardsRoleTreeByVpIdProc"('alice', false, 1)
ORDER BY "ChildTenantId", "ResultingChildRoleId";

SELECT 'CTA|' || "TenantId" || '|' || COALESCE("ParentTenantId"::text, '-') || '|' || "TenantName" ||
       '|' || COALESCE("DisplayName", '-') || '|' || COALESCE("TenantPassword", '-') ||
       '|' || COALESCE("TimeZone", '-') || '|' || COALESCE("TenantTypeId"::text, '-') ||
       '|' || COALESCE(("TenantDirty")::int::text, '-')
FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.A"]')
ORDER BY "TenantId";

SELECT 'CTB|' || "TenantId"
FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.B"]')
ORDER BY "TenantId";

SELECT 'CTC|' || "TenantId"
FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.C"]')
ORDER BY "TenantId";

SELECT 'CTV|' || "TenantId"
FROM "GetChildTenantsWithPermsByVpIdProc"('alice', false, 1, '["Perm.A"]')
ORDER BY "TenantId";

-- --- Erweiterung: die Faelle aus fixture2 ---

SELECT 'EF2|' || "RoleId" FROM "GetEffectiveTenantUserRoles"(2) ORDER BY "RoleId";

SELECT 'BOB|' || "OutermostLeafTenantId" || '|' || "ParentTenantId" || '|' || "ParentRoleId" || '|' ||
       "ParentLevel" || '|' || "TenantUserId" || '|' || "OutermostRoleId"
FROM "GetUpwardsRoleTreeForId"('bob', NULL::text)
ORDER BY "OutermostLeafTenantId", "ParentTenantId", "ParentLevel", "OutermostRoleId";

SELECT 'BDR|' || "ViewPointTenantId" || '|' || "ChildTenantId" || '|' || "ResultingChildRoleId" || '|' ||
       "ChildLevel" || '|' || "TopmostParentLevel"
FROM "GetDownwardsRoleTreeProc"('bob', false, 'T2')
ORDER BY "ChildTenantId", "ResultingChildRoleId";

SELECT 'CTD|' || "TenantId"
FROM "GetChildTenantsWithPermsProc"('alice', false, 'T1', '["Perm.D"]')
ORDER BY "TenantId";

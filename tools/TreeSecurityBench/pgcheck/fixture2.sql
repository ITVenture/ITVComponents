-- Erweiterung der Hierarchie um genau die Faelle, die die erste Fassung NICHT beruehrt hat.
-- Additiv: die bisherigen Zeilen bleiben, damit die schon geprueften Erwartungen gueltig bleiben.
--
--            T1 (alice)
--           /  |  \
--         T2   T4   (T2 traegt bob)
--        /  \
--      T3    T5

INSERT INTO "Tenants" ("TenantId","TenantName","DisplayName","ParentTenantId") VALUES
    (4,'T4','Zweig',1),
    (5,'T5','Zweites Blatt',2);

INSERT INTO "Users" ("Id","NormalizedUserName") VALUES ('bob','BOB');

-- bob haengt in der MITTE, nicht an der Wurzel: der Baum muss von dort aus rechnen.
INSERT INTO "TenantUsers" ("TenantUserId","TenantId","UserId") VALUES (2,2,'bob');

INSERT INTO "SecurityRoles" ("RoleId","TenantId","RoleName") VALUES
    (4,4,'R4'),
    (5,5,'R5'),
    (11,1,'R1b'),
    (22,2,'R2b');

INSERT INTO "TenantUserRoles" ("TenantUserId","RoleId") VALUES (2,2);

INSERT INTO "RoleRoles" ("PermissiveRoleId","PermittedRoleId") VALUES
    (4,1),    -- Verzweigung: R1 zieht auch R4 im Nachbarzweig nach sich
    (5,2),    -- zweites Blatt unter T2
    (11,1),   -- INNERHALB von T1: R1 zieht R1b nach sich -> Rekursion in GetEffectiveTenantUserRoles
    (44,11),  -- nur ueber R1b erreichbar - prueft die "diskrete Weitergabe"
    (22,1);   -- zweiter Weg nach T2 auf DERSELBEN Ebene -> Gleichstand, den RANK erhalten muss

INSERT INTO "SecurityRoles" ("RoleId","TenantId","RoleName") VALUES (44,4,'R4b');

INSERT INTO "Permissions" ("PermissionId","PermissionName") VALUES (4,'Perm.D');

-- Perm.D haengt allein an der Rolle, die nur ueber die mandanteninterne Weitergabe erreichbar ist.
INSERT INTO "RolePermissions" ("RoleId","PermissionId","TenantId") VALUES (44,4,4);

-- Wortgleiche Entsprechung zu pgcheck/fixture2.sql.
INSERT INTO Tenants (TenantId, TenantName, DisplayName, ParentTenantId) VALUES
    (4, 'T4', 'Zweig', 1),
    (5, 'T5', 'Zweites Blatt', 2);

INSERT INTO Users (Id, NormalizedUserName) VALUES ('bob', 'BOB');

INSERT INTO TenantUsers (TenantUserId, TenantId, UserId) VALUES (2, 2, 'bob');

INSERT INTO SecurityRoles (RoleId, TenantId, RoleName) VALUES
    (4, 4, 'R4'),
    (5, 5, 'R5'),
    (11, 1, 'R1b'),
    (22, 2, 'R2b');

INSERT INTO TenantUserRoles (TenantUserId, RoleId) VALUES (2, 2);

INSERT INTO RoleRoles (PermissiveRoleId, PermittedRoleId) VALUES
    (4, 1),
    (5, 2),
    (11, 1),
    (44, 11),
    (22, 1);

INSERT INTO SecurityRoles (RoleId, TenantId, RoleName) VALUES (44, 4, 'R4b');

INSERT INTO Permissions (PermissionId, PermissionName) VALUES (4, 'Perm.D');

INSERT INTO RolePermissions (RoleId, PermissionId, TenantId) VALUES (44, 4, 4);
GO

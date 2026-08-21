SET NOCOUNT ON;
-- Direkter Aufruf statt INSERT ... EXEC: die Prozedur macht intern selbst ein INSERT ... EXEC,
-- und T-SQL verbietet, das zu schachteln. Betrifft nur diesen Test-Aufbau - EF ruft die Prozedur
-- ueber FromSql, dort tritt die Schachtelung nie auf.
PRINT '#CTA';
EXEC GetChildTenantsWithPermsProc @UserId = 'alice', @UserIsLabels = 0,
     @ViewPointTenantName = 'T1', @RequiredPermissionArray = '["Perm.A"]';
PRINT '#CTB';
EXEC GetChildTenantsWithPermsProc @UserId = 'alice', @UserIsLabels = 0,
     @ViewPointTenantName = 'T1', @RequiredPermissionArray = '["Perm.B"]';
PRINT '#CTC';
EXEC GetChildTenantsWithPermsProc @UserId = 'alice', @UserIsLabels = 0,
     @ViewPointTenantName = 'T1', @RequiredPermissionArray = '["Perm.C"]';
PRINT '#CTV';
EXEC GetChildTenantsWithPermsByVpIdProc @UserId = 'alice', @UserIsLabels = 0,
     @ViewPointTenantId = 1, @RequiredPermissionArray = '["Perm.A"]';
PRINT '#CTD';
EXEC GetChildTenantsWithPermsProc @UserId = 'alice', @UserIsLabels = 0,
     @ViewPointTenantName = 'T1', @RequiredPermissionArray = '["Perm.D"]';

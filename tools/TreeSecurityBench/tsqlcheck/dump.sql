SET NOCOUNT ON;

-- Zeilenweiser Abzug, wortgleich zur PostgreSQL-Seite aufgebaut. Die Prozeduren muessen den Umweg
-- ueber eine temporaere Tabelle nehmen: T-SQL kann eine Prozedur nicht in einer Abfrage verwenden -
-- genau der Unterschied, wegen dem es auf PostgreSQL Funktionen sind.

SELECT CONCAT('UPT|', OutermostLeafTenantId, '|', OutermostLeafTenantName, '|',
              ParentTenantId, '|', ParentTenantName, '|', ParentLevel)
FROM UpwardsTenantTree
ORDER BY OutermostLeafTenantId, ParentTenantId, ParentLevel;

SELECT CONCAT('DNT|', TopmostTenantId, '|', TopmostTenantName, '|',
              ChildTenantId, '|', ChildTenantName, '|', ChildLevel)
FROM DownwardsTenantTree
ORDER BY TopmostTenantId, ChildTenantId, ChildLevel;

SELECT CONCAT('TAT|', OutermostLeafTenantId, '|', OutermostLeafTenantName, '|',
              ParentTenantId, '|', ParentTenantName, '|', ParentLevel, '|',
              topmosttenantid, '|', TopmostTenantName, '|',
              childtenantid, '|', childtenantname, '|',
              tenantuserid, '|', userid, '|', CAST(directAssign AS int))
FROM TenantAccessTree
ORDER BY OutermostLeafTenantId, ParentTenantId, childtenantid, tenantuserid;

SELECT CONCAT('EFF|', RoleId)
FROM dbo.GetEffectiveTenantUserRoles(1)
ORDER BY RoleId;

SELECT CONCAT('URI|', OutermostLeafTenantId, '|', OutermostLeafTenantName, '|',
              ParentTenantId, '|', ParentTenantName, '|', ParentRoleId, '|',
              ParentLevel, '|', TenantUserId, '|', UserId, '|', OutermostRoleId)
FROM dbo.GetUpwardsRoleTreeForId('alice', NULL)
ORDER BY OutermostLeafTenantId, ParentTenantId, ParentLevel, OutermostRoleId;

SELECT CONCAT('URL|', OutermostLeafTenantId, '|', OutermostLeafTenantName, '|',
              ParentTenantId, '|', ParentTenantName, '|', ParentRoleId, '|',
              ParentLevel, '|', TenantUserId, '|', UserId, '|', OutermostRoleId)
FROM dbo.GetUpwardsRoleTreeForLabels('["ALICE"]', NULL)
ORDER BY OutermostLeafTenantId, ParentTenantId, ParentLevel, OutermostRoleId;

SELECT CONCAT('URB|', OutermostLeafTenantId, '|', ParentTenantId, '|',
              ParentRoleId, '|', ParentLevel, '|', OutermostRoleId)
FROM dbo.GetUpwardsRoleTreeForIdByLeafId('alice', 3)
ORDER BY OutermostLeafTenantId, ParentTenantId, ParentLevel, OutermostRoleId;

CREATE TABLE #drt (ViewpointTenantId int, ViewpointTenantName nvarchar(100), TopmostTenantId int,
                   TopmostTenantName nvarchar(100), ChildTenantId int, ChildTenantName nvarchar(100),
                   TenantUserId int, UserId nvarchar(100), ResultingChildRoleId int,
                   ChildLevel int, TopmostParentLevel int);
INSERT INTO #drt EXEC GetDownwardsRoleTreeProc @pUserId = 'alice', @puserIsLabels = 0, @pViewPoint = 'T1';
SELECT CONCAT('DRT|', ViewpointTenantId, '|', ViewpointTenantName, '|',
              TopmostTenantId, '|', TopmostTenantName, '|',
              ChildTenantId, '|', ChildTenantName, '|',
              TenantUserId, '|', UserId, '|', ResultingChildRoleId, '|',
              ChildLevel, '|', TopmostParentLevel)
FROM #drt
ORDER BY ChildTenantId, ResultingChildRoleId, TopmostTenantId;

CREATE TABLE #drl (ViewpointTenantId int, ViewpointTenantName nvarchar(100), TopmostTenantId int,
                   TopmostTenantName nvarchar(100), ChildTenantId int, ChildTenantName nvarchar(100),
                   TenantUserId int, UserId nvarchar(100), ResultingChildRoleId int,
                   ChildLevel int, TopmostParentLevel int);
INSERT INTO #drl EXEC GetDownwardsRoleTreeProc @pUserId = '["ALICE"]', @puserIsLabels = 1, @pViewPoint = 'T1';
SELECT CONCAT('DRL|', ViewpointTenantId, '|', ChildTenantId, '|',
              ResultingChildRoleId, '|', ChildLevel, '|', TopmostParentLevel)
FROM #drl
ORDER BY ChildTenantId, ResultingChildRoleId;

CREATE TABLE #drv (ViewpointTenantId int, ViewpointTenantName nvarchar(100), TopmostTenantId int,
                   TopmostTenantName nvarchar(100), ChildTenantId int, ChildTenantName nvarchar(100),
                   TenantUserId int, UserId nvarchar(100), ResultingChildRoleId int,
                   ChildLevel int, TopmostParentLevel int);
INSERT INTO #drv EXEC GetDownwardsRoleTreeByVpIdProc @pUserId = 'alice', @puserIsLabels = 0, @pViewPointTenantId = 1;
SELECT CONCAT('DRV|', ViewpointTenantId, '|', ChildTenantId, '|',
              ResultingChildRoleId, '|', ChildLevel, '|', TopmostParentLevel)
FROM #drv
ORDER BY ChildTenantId, ResultingChildRoleId;

CREATE TABLE #cta (TenantId int, ParentTenantId int, TenantName nvarchar(150), DisplayName nvarchar(1024),
                   TenantPassword nvarchar(125), TimeZone nvarchar(1024), TenantTypeId int, TenantDirty bit);
INSERT INTO #cta EXEC GetChildTenantsWithPermsProc @UserId = 'alice', @UserIsLabels = 0,
                                                   @ViewPointTenantName = 'T1', @RequiredPermissionArray = '["Perm.A"]';
SELECT CONCAT('CTA|', TenantId, '|', ISNULL(CAST(ParentTenantId AS nvarchar(20)), '-'), '|', TenantName,
              '|', ISNULL(DisplayName, '-'), '|', ISNULL(TenantPassword, '-'),
              '|', ISNULL(TimeZone, '-'), '|', ISNULL(CAST(TenantTypeId AS nvarchar(20)), '-'),
              '|', ISNULL(CAST(CAST(TenantDirty AS int) AS nvarchar(20)), '-'))
FROM #cta
ORDER BY TenantId;

CREATE TABLE #ctb (TenantId int, ParentTenantId int, TenantName nvarchar(150), DisplayName nvarchar(1024),
                   TenantPassword nvarchar(125), TimeZone nvarchar(1024), TenantTypeId int, TenantDirty bit);
INSERT INTO #ctb EXEC GetChildTenantsWithPermsProc @UserId = 'alice', @UserIsLabels = 0,
                                                   @ViewPointTenantName = 'T1', @RequiredPermissionArray = '["Perm.B"]';
SELECT CONCAT('CTB|', TenantId) FROM #ctb ORDER BY TenantId;

CREATE TABLE #ctc (TenantId int, ParentTenantId int, TenantName nvarchar(150), DisplayName nvarchar(1024),
                   TenantPassword nvarchar(125), TimeZone nvarchar(1024), TenantTypeId int, TenantDirty bit);
INSERT INTO #ctc EXEC GetChildTenantsWithPermsProc @UserId = 'alice', @UserIsLabels = 0,
                                                   @ViewPointTenantName = 'T1', @RequiredPermissionArray = '["Perm.C"]';
SELECT CONCAT('CTC|', TenantId) FROM #ctc ORDER BY TenantId;

CREATE TABLE #ctv (TenantId int, ParentTenantId int, TenantName nvarchar(150), DisplayName nvarchar(1024),
                   TenantPassword nvarchar(125), TimeZone nvarchar(1024), TenantTypeId int, TenantDirty bit);
INSERT INTO #ctv EXEC GetChildTenantsWithPermsByVpIdProc @UserId = 'alice', @UserIsLabels = 0,
                                                         @ViewPointTenantId = 1, @RequiredPermissionArray = '["Perm.A"]';
SELECT CONCAT('CTV|', TenantId) FROM #ctv ORDER BY TenantId;

SELECT CONCAT('EF2|', RoleId) FROM dbo.GetEffectiveTenantUserRoles(2) ORDER BY RoleId;

SELECT CONCAT('BOB|', OutermostLeafTenantId, '|', ParentTenantId, '|', ParentRoleId, '|',
              ParentLevel, '|', TenantUserId, '|', OutermostRoleId)
FROM dbo.GetUpwardsRoleTreeForId('bob', NULL)
ORDER BY OutermostLeafTenantId, ParentTenantId, ParentLevel, OutermostRoleId;

CREATE TABLE #bdr (ViewpointTenantId int, ViewpointTenantName nvarchar(100), TopmostTenantId int,
                   TopmostTenantName nvarchar(100), ChildTenantId int, ChildTenantName nvarchar(100),
                   TenantUserId int, UserId nvarchar(100), ResultingChildRoleId int,
                   ChildLevel int, TopmostParentLevel int);
INSERT INTO #bdr EXEC GetDownwardsRoleTreeProc @pUserId = 'bob', @puserIsLabels = 0, @pViewPoint = 'T2';
SELECT CONCAT('BDR|', ViewpointTenantId, '|', ChildTenantId, '|', ResultingChildRoleId, '|',
              ChildLevel, '|', TopmostParentLevel)
FROM #bdr
ORDER BY ChildTenantId, ResultingChildRoleId;

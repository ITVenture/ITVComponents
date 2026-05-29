using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Options;
using ITVComponents.Json;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Microsoft.Data.SqlClient;
using System.Linq;
using TargetInterface = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.IHierarchySecurityContext<ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.HierarchyTenant, string, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.User, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.Role, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.Permission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.UserRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.RolePermission,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.HierarchyTenantUser, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.RoleRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.GlobalRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.GlobalRolePermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.GRoleLRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.NavigationMenu, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.TenantNavigationMenu, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DiagnosticsQuery,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DiagnosticsQueryParameter, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.TenantDiagnosticsQuery, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DashboardWidget, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DashboardParam,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DashboardWidgetLocalization, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.UserWidget
    , ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.CustomUserProperty, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplate, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplatePath, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplateGrant, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplateFeature,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.SharedAsset, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.SharedAssetUserFilter, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.SharedAssetTenantFilter, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppTemplate, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AppPermission,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AppPermissionSet, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppTemplatePermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientApp, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppPermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppUser,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyWebPlugin, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyWebPluginConstant, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyWebPluginGenericParameter,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchySequence, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyTenantSetting, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyTenantFeatureActivation,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyExternalOAuthService, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyExternalOAuthServiceState, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyExternalOAuthServiceTenantLogin,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models.HierarchyTenantContextSecurityTrustConfig>;
namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.SqlServer.SyntaxHelper
{
    public static class SqlColumnsSyntaxHelper
    {
        public static void ConfigureComputedColumns<TContext>(DbContextModelBuilderOptions<TContext> builderOptions)
        {
            builderOptions.ConfigureComputedColumn<NavigationMenu, string>(n => n.UrlUniqueness,
                "case when isnull(Url,'')='' and isnull(RefTag,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) when isnull(Url,'')='' then RefTag else Url end persisted");
            builderOptions.ConfigureComputedColumn<Role, string>(r => r.RoleNameUniqueness,
                "'__T'+convert(varchar(10),TenantId)+'##'+RoleName persisted");
            builderOptions.ConfigureComputedColumn<Permission, string>(p => p.PermissionNameUniqueness,
                "case when TenantId is null then PermissionName else '__T'+convert(varchar(10),TenantId)+'##'+PermissionName end persisted");
            builderOptions.ConfigureComputedColumn<HierarchyWebPlugin, string>(w => w.PluginNameUniqueness,
                "case when TenantId is null then UniqueName else '__T'+convert(varchar(10),TenantId)+'##'+UniqueName end persisted");
            builderOptions.ConfigureComputedColumn<HierarchyWebPluginConstant, string>(c => c.NameUniqueness,
                "case when TenantId is null then Name else '__T'+convert(varchar(10),TenantId)+'##'+Name end persisted");
            builderOptions.ConfigureComputedColumn<HierarchyExternalOAuthService, string>(s => s.CalculatedUniqueServiceName, "case when TenantId is null then 'GLOBAL'+UniqueConnectionName else 'T'+convert(varchar(10),TenantId)+'-'+UniqueConnectionName end persisted");
            ConfigureVirtualTables(builderOptions);
        }

        public static void ConfigureVirtualTables(IContextModelBuilderOptions builderOptions)
        {
            ConfigureUpwardsTree(builderOptions);
            ConfigureDownwardsTree(builderOptions);
            ConfigureAccessTree(builderOptions);
            ConfigureUpwardsRoleTree(builderOptions);
            ConfigureDownwardsRoleTree(builderOptions);
        }

        public static void ConfigureUpwardsTree(IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureEntity<UpwardsTenantView>(uu =>
                uu.ToView(GlobalDbObjectNaming.UpwardsTenantTreeView).HasNoKey());
        }

        public static void ConfigureAccessTree(IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureEntity<UserAccessTree<string>>(uat => uat.ToView(GlobalDbObjectNaming.UserAccessTree).HasNoKey());
        }

        public static void ConfigureUpwardsRoleTree(
            IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureDbFunction("GetUpwardsRoleTreeForId",
                m => m.HasName("GetUpwardsRoleTreeForId"));
            builderOptions.ConfigureDbFunction("GetUpwardsRoleTreeForLabels",
                m => m.HasName("GetUpwardsRoleTreeForLabels"));
            builderOptions.ConfigureDbFunction("GetUpwardsRoleTreeForIdByLeafId",
                m => m.HasName("GetUpwardsRoleTreeForIdByLeafId"));
            builderOptions.ConfigureDbFunction("GetUpwardsRoleTreeForLabelsByLeafId",
                m => m.HasName("GetUpwardsRoleTreeForLabelsByLeafId"));

            builderOptions.ConfigureEntity<UpwardsRoleUserView<string>>(b => b.HasNoKey());
            /*builderOptions.ConfigureEntity<UpwardsRoleUserView<string>>(pp =>
                pp.ToTable(GlobalDbObjectNaming.UpwardsRoleTreeView, b => b.ExcludeFromMigrations()).HasNoKey());*/
        }

        public static void ConfigureDownwardsTree(
            IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureEntity<DownwardsTenantView>(dd =>
                dd.ToView(GlobalDbObjectNaming.DownwardsTenantTreeView).HasNoKey());
        }

        public static void ConfigureDownwardsRoleTree(
            IContextModelBuilderOptions builderOptions)
        {
            /*builderOptions.ConfigureDbFunction("GetDownwardsRoleTree",
                m => m.HasName("GetDownwardsRoleTree"));*/
            builderOptions.ConfigureEntity<DownwardsUserRoleView<string>>(b => b.HasNoKey().ToView(null));
            /*builderOptions.ConfigureEntity<DownwardsUserRoleView<string>>(puv =>
                puv.ToTable(GlobalDbObjectNaming.DownwardsRoleTreeView, b => b.ExcludeFromMigrations())
                    .HasNoKey());*/
        }

        public static void ConfigureMethods(IContextModelBuilderOptions bld)
        {
            bld.ConfigureMethod("SequenceNextVal", (DbContext c, string name, int tenantId) =>
            {
                var tmp = c.Database.SqlQuery<ValueTableModel<int>>(@"declare @vld table (Value int)
update Sequences set CurrentValue = case when CurrentValue+StepSize<=MaxValue then CurrentValue+StepSize when CurrentValue+StepSize > MaxValue and Cycle=1 then MinValue else -1 end 
output inserted.CurrentValue Value
into @vld
where SequenceName = @name and TenantId = @tenantId
select * from @vld", new SqlParameter("@name", name),
                    new Microsoft.Data.SqlClient.SqlParameter("@tenantId", tenantId));
                return tmp.First().Value;
            });

            ConfigureChildTenantPermissionMethod(bld);
        }

        public static void ConfigureChildTenantPermissionMethod(IContextModelBuilderOptions bld)
        {
            /*oldStratStart*/
            /*var ctx = (TargetInterface)c;
                    var tmpUserQuery = GetRawUserQuery(ctx, userId, currentTenant, out var fullUserList);
                    var clr = fullUserList.Select(n => n.ResultingChildRoleId).ToArray();
                    var tmpRolePermissions = (from r in ctx.SecurityRoles.Where(n => clr.Contains(n.RoleId))
                        join rp in ctx.RolePermissions on new { r.TenantId, r.RoleId } equals new
                            { rp.TenantId, rp.RoleId }
                        join p in ctx.Permissions on rp.PermissionId equals p.PermissionId
                        where requiredPermissions.Contains(p.PermissionName)
                        select new { r.Tenant, r.RoleId }).ToList();

                    var rv = (from t in fullUserList
                        join urq in tmpUserQuery on t.TenantUserId equals urq.TenantUserId
                        join r in tmpRolePermissions on new { TenantId = t.ChildTenantId, RoleId = t.ResultingChildRoleId } equals new
                            { r.Tenant.TenantId, r.RoleId } select r.Tenant
                        ).Distinct().ToArray();

                    return rv;*/
            /*oldStratEnd -- copy into body */
            bld.ConfigureMethod<Func<DbContext, string, string, string[], HierarchyTenant[]>>(GlobalDbObjectNaming.ChildTenantsWithProc,
                (c, userId, currentTenant, requiredPermissions) =>
                {
                    var ctx = (TargetInterface)c;
                    return ctx.Tenants
                        .FromSql(
                            $"exec GetChildTenantsWithPermsProc @UserId={userId},@UserIsLabels=0,@ViewPointTenantName={currentTenant},@RequiredPermissionArray={JsonHelper.ToJson(requiredPermissions, SerializationTypingMode.StaticTyping)}")
                        .ToArray();
                });

            bld.ConfigureMethod<Func<DbContext, string[], string, string[], HierarchyTenant[]>>(GlobalDbObjectNaming.ChildTenantsWithProc,
                (c, userLabels, currentTenant, requiredPermissions) =>
                {
                    var ctx = (TargetInterface)c;
                    return ctx.Tenants
                        .FromSql(
                            $"exec GetChildTenantsWithPermsProc @UserId={JsonHelper.ToJson(userLabels, SerializationTypingMode.StaticTyping)},@UserIsLabels=1,@ViewPointTenantName={currentTenant},@RequiredPermissionArray={JsonHelper.ToJson(requiredPermissions, SerializationTypingMode.StaticTyping)}")
                        .ToArray();
                });

            //--

            bld.ConfigureMethod<Func<DbContext, string, int?, string[], HierarchyTenant[]>>(GlobalDbObjectNaming.ChildTenantsWithProc,
                (c, userId, currentTenant, requiredPermissions) =>
                {
                    var ctx = (TargetInterface)c;
                    return ctx.Tenants
                        .FromSql(
                            $"exec GetChildTenantsWithPermsByVpIProc @UserId={userId},@UserIsLabels=0,@ViewPointTenantId={currentTenant},@RequiredPermissionArray={JsonHelper.ToJson(requiredPermissions, SerializationTypingMode.StaticTyping)}")
                        .ToArray();
                });

            bld.ConfigureMethod<Func<DbContext, string[], int?, string[], HierarchyTenant[]>>(GlobalDbObjectNaming.ChildTenantsWithProc,
                (c, userLabels, currentTenant, requiredPermissions) =>
                {
                    var ctx = (TargetInterface)c;
                    return ctx.Tenants
                        .FromSql(
                            $"exec GetChildTenantsWithPermsByVpIProc @UserId={JsonHelper.ToJson(userLabels, SerializationTypingMode.StaticTyping)},@UserIsLabels=1,@ViewPointTenantId={currentTenant},@RequiredPermissionArray={JsonHelper.ToJson(requiredPermissions, SerializationTypingMode.StaticTyping)}")
                        .ToArray();
                });
        }

        public static void ConfigureViews(MigrationBuilder migrationBuilder, string schema = "dbo")
        {
            migrationBuilder.Sql($"Drop View if exists [{schema}].[TenantAccessTreeUp]");
            migrationBuilder.Sql($"Drop View if exists [{schema}].[TenantAccessTreeDown]");
            migrationBuilder.Sql($@"DROP VIEW if exists [{schema}].[UpwardsTenantTree]");
                migrationBuilder.Sql($@"DROP VIEW if exists [{schema}].[DownwardsTenantTree]");
                //migrationBuilder.Sql($@"DROP FUNCTION [{schema}].[GetUpwardsRoleTree]");
                migrationBuilder.Sql($@"DROP FUNCTION if exists [{schema}].[GetUpwardsRoleTreeForLabels]");
                migrationBuilder.Sql($@"DROP FUNCTION if exists [{schema}].[GetUpwardsRoleTreeForId]");
                migrationBuilder.Sql($@"DROP PROCEDURE if exists [{schema}].[GetDownwardsRoleTreeProc]");
                migrationBuilder.Sql($@"DROP PROCEDURE if exists [{schema}].[GetChildTenantsWithPermsProc]");
                //--
                migrationBuilder.Sql($@"DROP FUNCTION if exists [{schema}].[GetUpwardsRoleTreeForLabelsByLeafId]");
                migrationBuilder.Sql($@"DROP FUNCTION if exists [{schema}].[GetUpwardsRoleTreeForIdByLeafId]");
                migrationBuilder.Sql($@"DROP PROCEDURE if exists [{schema}].[GetDownwardsRoleTreeByVpIdProc]");
                migrationBuilder.Sql($@"DROP PROCEDURE if exists [{schema}].[GetChildTenantsWithPermsByVpIdProc]");

            migrationBuilder.Sql($@"CREATE VIEW [{schema}].[UpwardsTenantTree]
AS
WITH r AS (SELECT   TenantId AS OutermostLeafTenantId, TenantName AS OutermostLeafTenantName, TenantId AS ParentTenantId, TenantName AS ParentTenantName, 1 AS ParentLevel, 
                           ParentTenantId AS NextParent
 FROM         {schema}.Tenants
 UNION ALL
 SELECT   r_2.OutermostLeafTenantId, r_2.OutermostLeafTenantName, u.TenantId AS ParentTenantId, u.TenantName AS ParentTenantName, r_2.ParentLevel + 1 AS ParentLevel, 
                          u.ParentTenantId AS NextParent
 FROM         {schema}.Tenants AS u INNER JOIN
                          r AS r_2 ON u.TenantId = r_2.NextParent)
SELECT   OutermostLeafTenantId, OutermostLeafTenantName, ParentTenantId, ParentTenantName, ParentLevel
FROM         r AS r_1");

            migrationBuilder.Sql($@"CREATE VIEW [{schema}].[DownwardsTenantTree]
AS
WITH r AS (SELECT   TenantId AS TopmostTenantId, TenantName AS TopmostTenantName, TenantId AS ChildTenantId, TenantName AS ChildTenantName, 1 AS ChildLevel
                             FROM         {schema}.Tenants
                             UNION ALL
                             SELECT   r.TopmostTenantId, r.TopmostTenantName, u.TenantId AS ChildTenantId, u.TenantName AS ChildTenantName, r.ChildLevel + 1 AS ChildLevel
                             FROM         {schema}.Tenants AS u INNER JOIN
                                                      r  ON r.ChildTenantId = u.ParentTenantId)
    SELECT   TopmostTenantId,TopmostTenantName, ChildTenantId, ChildTenantName, ChildLevel
     FROM         r");

            migrationBuilder.Sql($$"""
                                   create view [{{schema}}].[TenantAccessTreeDown] as 
                                   with rDown as (select t.TenantId as TopmostTenantId, t.TenantName as TopmostTenantName, s.RoleId TopmostSecurityRole, tu.TenantUserId, tu.UserId,
                                   t.TenantId as ChildTenantId, t.TenantName as ChildTenantName, s.RoleId ChildTenantRole, s.RoleId NextParentRoleId, level = 1 from Tenants t
                                   inner join TenantUsers tu on tu.TenantId = t.TenantId
                                   inner join SecurityRoles s on s.TenantId = t.TenantId
                                   inner join TenantUserRoles tur on tur.TenantUserId = tu.TenantUserId and tur.RoleId = s.RoleId
                                   union all
                                   select r_2.TopmostTenantId, r_2.TopmostTenantName, r_2.TopmostSecurityRole, r_2.TenantUserId, r_2.UserId, 
                                   tc.TenantId as ChildTenantId, tc.TenantName as ChildTenantName, ts.RoleId ChildTenantRoleId, ts.RoleId NextParentRoleId, level+1 level
                                   from
                                   rDown r_2
                                   inner join Tenants tc on tc.ParentTenantId = r_2.ChildTenantId
                                   inner join SecurityRoles ts on ts.TenantId = tc.TenantId
                                   inner join RoleRoles tcr on tcr.PermittedRoleId = r_2.NextParentRoleId and tcr.PermissiveRoleId = ts.RoleId
                                   )
                                   
                                   select * from rDown
                                   """);

            migrationBuilder.Sql($$"""
                                   create view [{{schema}}].[TenantAccessTreeUp] as 
                                   with rUp as (select s.RoleId, s.RoleId ParentRoleId, s.TenantId, s.TenantId ParentTenantId, st.ParentTenantId as nextparent, s.RoleId as nextChildRole, level = 1 
                                   	   from 
                                   	   SecurityRoles s inner join Tenants st on st.TenantId = s.TenantId
                                   union all
                                   select r_2.RoleId, pr.RoleId ParentRoleId, r_2.TenantId, pr.TenantId ParentTenantId, pt.ParentTenantId as nextparent, pr.RoleId as nextChildRole, r_2.level+1 level 
                                   from rUp as r_2
                                   inner join RoleRoles roro on r_2.nextChildRole = roro.PermissiveRoleId
                                   inner join SecurityRoles pr on pr.RoleId = roro.PermittedRoleId and pr.TenantId =r_2.nextparent
                                   inner join Tenants ct on ct.TenantId = r_2.TenantId
                                   inner join Tenants pt on pt.TenantId = pr.TenantId and pr.TenantId = r_2.nextparent
                                   )
                                   select * from rUp
                                   """);

            migrationBuilder.Sql($$"""
                                   create view [{{schema}}].[TenantAccessTree] as 
                                   with ranked as (select t.OutermostLeafTenantId, OutermostLeafTenantName, t.ParentTenantId, t.ParentTenantName, t.ParentLevel, 
                                   					d.topmosttenantid, d.topmosttenantname, d.tenantuserid, d.userid, d.childtenantid, d.childtenantname, d.level, ROW_NUMBER() OVER (
                                               PARTITION BY d.userid, outermostleaftenantid, d.childtenantid
                                               ORDER BY t.parentlevel DESC  -- lokal gewinnt
                                           ) AS rn from UpwardsTenantTree t
                                   inner join TenantAccessTreeDown d on d.ChildTenantId = t.ParentTenantId 
                                   left outer join (TenantAccessTreeUp u  inner join tenantusers tut on tut.tenantid = u.parenttenantid) on d.childtenantid = u.parenttenantid 
                                   and d.childtenantrole = u.parentRoleId and tut.UserId = d.userid and d.childtenantid = t.OutermostLeafTenantId
                                   )
                                   select OutermostLeafTenantId, OutermostLeafTenantName, ParentTenantId, ParentTenantName, ParentLevel, topmosttenantid, TopmostTenantName, tenantuserid, userid, childtenantid, childtenantname, case when [level] = 1 then CAST(1 as bit) else cast(0 as bit) end directAssign from ranked 
                                   where rn = 1
                                   """);

            if (false)
            {
                migrationBuilder.Sql($@"CREATE VIEW [{schema}].[UpwardsRoleTree]
AS
WITH r AS (SELECT   t.TenantId AS OutermostLeafTenantId, TenantName AS OutermostLeafTenantName, t.TenantId AS ParentTenantId, TenantName AS ParentTenantName, 1 AS ParentLevel, 
                           ParentTenantId AS NextParent, t.TenantId as CurParent, null as prevParent, sr.roleid as outermostrole, sr.roleid as currentrole
 FROM         [{schema}].Tenants t inner join securityroles sr on sr.tenantid = t.TenantId
 UNION ALL
 SELECT   r_2.OutermostLeafTenantId, r_2.OutermostLeafTenantName, u.TenantId AS ParentTenantId, u.TenantName AS ParentTenantName, r_2.ParentLevel + 1 AS ParentLevel, 
                          u.ParentTenantId AS NextParent, u.tenantid as CurParent, r_2.curparent as prevParent, r_2.outermostrole, pr.roleid as currentrole
 FROM        [{schema}].Tenants AS u INNER JOIN
                          r AS r_2 ON u.TenantId = r_2.NextParent
						  inner join securityroles pr on pr.tenantid = u.TenantId
						  inner join securityroles cr on (cr.tenantid = r_2.CurParent and cr.roleid = r_2.currentrole) or cr.roleid = pr.RoleId
						  inner join roleroles roro on roro.PermissiveRoleId = cr.roleid and roro.PermittedRoleId = pr.RoleId)
SELECT   OutermostLeafTenantId, OutermostLeafTenantName, ParentTenantId, parenttenantname, parentlevel, tu.TenantUserId, u.id as UserId, OutermostRole OutermostRoleId
FROM         r AS r_1
inner join securityroles pr on pr.roleid = r_1.currentrole
inner join securityroles cr on cr.roleid = r_1.outermostrole
inner join tenantuserroles tur on tur.roleid in (pr.roleid, cr.RoleId)
inner join tenantusers tu on tu.tenantuserid = tur.TenantUserId
inner join users u on u.id = tu.UserId");

                migrationBuilder.Sql($@"CREATE VIEW [{schema}].[DownwardsRoleTree]
as
WITH z AS (SELECT   t.TenantId AS TopmostTenantId, TenantName AS TopmostTenantName, t.TenantId AS ChildTenantId, TenantName AS ChildTenantName, 1 AS ChildLevel, 
                           sr.roleid as topmostRoleId, sr.roleid as currentrole, t.TenantId as currentTenant
 FROM         [{schema}].Tenants t inner join securityroles sr on sr.tenantid = t.TenantId
 UNION ALL
 SELECT   z_2.TopmostTenantId, z_2.TopmostTenantName, u.TenantId AS ChildTenantId, u.TenantName AS ChildTenantName, z_2.ChildLevel + 1 AS ChildLevel, 
                          z_2.topmostRoleId, pr.roleid as currentrole, u.TenantId as currentTenant
 FROM        [{schema}].Tenants AS u INNER JOIN
                          z AS z_2 ON u.ParentTenantId= z_2.currentTenant
						  inner join securityroles pr on pr.tenantid = u.TenantId
						  inner join securityroles cr on (cr.tenantid = z_2.currentTenant and cr.roleid = z_2.currentrole) or cr.roleid = pr.RoleId
						  inner join roleroles roro on roro.PermissiveRoleId = pr.roleid and roro.PermittedRoleId = cr.RoleId)

SELECT   TopmostTenantId ViewPointTenantId, TopmostTenantName ViewPointTenantName, ChildTenantId, ChildTenantName, ChildLevel, r.TenantUserId, u.id as UserId,
cr.roleid ResultingChildRoleId, r.ParentTenantId TopmostTenantId, r.parentTenantName TopmostTenantName, r.ParentLevel as TopmostParentLevel
FROM         z AS z_1  
inner join [UpwardsRoleTree] r
on z_1.TopmostTenantId = r.OutermostLeafTenantId and z_1.topmostRoleId = r.OutermostRoleId
inner join securityroles pr on pr.roleid = r.OutermostRoleId
inner join securityroles cr on cr.roleid = z_1.currentrole
inner join tenantuserroles tur on tur.roleid in (pr.roleid, cr.RoleId)
inner join tenantusers tu on tu.tenantuserid = tur.TenantUserId and tu.tenantuserid = r.tenantuserid
inner join users u on u.id = tu.UserId");
            }
            else
            {
                var drt = $$"""
                             Create Procedure [{{schema}}].[GetDownwardsRoleTreeProc] 
                             (	
                             	@pUserId NVARCHAR(256),
                             	@puserIsLabels bit,
                             	@pViewPoint nvarchar(256)
                             )
                             AS
                             begin
                             	declare @UserId nvarchar(256) = @pUserId
                             	declare @userIsLabels bit = @puserIsLabels
                             	declare @viewPoint nvarchar(256) = @pViewPoint
                             	declare @completeUpTree table(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(100), ParentTenantId int, ParentTenantName nvarchar(100), ParentRoleId int, ParentLevel int , TenantUserId int, UserId nvarchar(100), OutermostRoleId int)
                             	declare @completeUpTreeShrinker table(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(100), ParentLevel int, UserId nvarchar(100))
                             	declare @resultingUpTree table(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(100), ParentTenantId int, ParentTenantName nvarchar(100), ParentRoleId int, ParentLevel int , TenantUserId int, UserId nvarchar(100), OutermostRoleId int)
                             	if (@userIsLabels=1) begin
                             		insert into @completeUpTree select * from GetUpwardsRoleTreeForLabels(@userid, @viewPoint) --group by OutermostLeafTenantId, OutermostLeafTenantName, UserId
                             	end else begin
                             		insert into @completeUpTree select * from GetUpwardsRoleTreeForId(@userid, @viewPoint) --group by OutermostLeafTenantId, OutermostLeafTenantName, UserId
                             	end
                             	insert into @completeUpTreeShrinker (OutermostLeafTenantId, OutermostLeafTenantName, UserId, ParentLevel) select OutermostLeafTenantId, OutermostLeafTenantName, UserId, min(parentlevel) as parentLevel from @completeUpTree group by OutermostLeafTenantId, OutermostLeafTenantName, UserId
                             	insert into @resultingUpTree select b.* from @completeUpTreeShrinker a inner join @completeUpTree b on a.OutermostLeafTenantId = b.OutermostLeafTenantId and a.OutermostLeafTenantName = b.OutermostLeafTenantName and a.ParentLevel = b.ParentLevel and a.UserId = b.UserId
                             	declare @rawtree table (
                             		TopmostTenantId int, 
                             		TopmostTenantName nvarchar(100), 
                             		TopmostRoleId int,
                             		ViewpointTenantId int, 
                             		ViewpointTenantName nvarchar(100), 
                             		ChildTenantId int, 
                             		ChildTenantName nvarchar(100), 
                             		ChildLevel int, 
                             		UserId nvarchar(100), 
                             		OutermostRoleId int
                             	)
                             
                             	insert into @rawTree
                             	SELECT   ParentTenantId TopmostTenantId, ParentTenantName TopmostTenantName, ParentRoleId TopMostRoleId, a.OutermostLeafTenantId ViewpointTenantId, a.OutermostLeafTenantName ViewpointTenantName, d.childtenantid, d.ChildTenantName, d.ChildLevel, UserId, OutermostRoleId--OutermostLeafTenantId, OutermostLeafTenantName, ParentTenantId, parenttenantname, parentlevel, tu.TenantUserId, u.id as UserId, OutermostRole OutermostRoleId
                             	from @resultingUpTree a
                             	inner join downwardstenanttree d on d.TopmostTenantId = OutermostLeafTenantId
                             	;
                             
                             	with r as (select s.RoleId, s.RoleId ParentRoleId, s.TenantId, s.TenantId ParentTenantId, st.ParentTenantId as nextparent, s.RoleId as nextChildRole, level = 1 from SecurityRoles s inner join Tenants st on st.TenantId = s.TenantId
                             union all
                             select r_2.RoleId, pr.RoleId ParentRoleId, r_2.TenantId, pr.TenantId ParentTenantId, pt.ParentTenantId as nextparent, pr.RoleId as nextChildRole, r_2.level+1 level from r as r_2
                             inner join RoleRoles roro on r_2.nextChildRole = roro.PermissiveRoleId
                             inner join SecurityRoles pr on pr.RoleId = roro.PermittedRoleId and pr.TenantId =r_2.nextparent
                             inner join Tenants ct on ct.TenantId = r_2.TenantId
                             inner join Tenants pt on pt.TenantId = pr.TenantId and pr.TenantId = r_2.nextparent)
                             
                             
                             SELECT   d.ViewpointTenantId, d.ViewpointTenantName, r.ParentTenantId TopmostTenantId, parenttenantname TopmostTenantName, OutermostLeafTenantId ChildTenantId, OutermostLeafTenantName ChildTenantName, tu.TenantUserId, u.id as UserId, r.RoleId ResultingChildRoleId, d.ChildLevel, parentlevel TopmostParentLevel from UpwardsTenantTree t
                             inner join r on r.TenantId = t.OutermostLeafTenantId and r.ParentTenantId = t.ParentTenantId
                             inner join TenantUsers tu on tu.TenantId = t.ParentTenantId
                             inner join Users u on u.id = tu.UserId
                             inner join SecurityRoles cr on cr.TenantId = r.TenantId and cr.RoleId = r.RoleId
                             inner join SecurityRoles pr on pr.TenantId = r.ParentTenantId and pr.RoleId = r.ParentRoleId
                             inner join TenantUserRoles tur on tur.TenantUserId = tu.TenantUserId and tur.RoleId = pr.RoleId
                             inner join @rawtree d on d.TopmostTenantId = r.ParentTenantId and d.ChildTenantId = OutermostLeafTenantId and d.UserId = u.Id and d.TopmostRoleId = pr.RoleId
                             
                             end
                             """;
                var ctwp = $$"""
                           CREATE procedure [{{schema}}].[GetChildTenantsWithPermsProc]
                           (
                               @UserId nvarchar(100),
                           	@UserIsLabels bit,
                           	@ViewPointTenantName nvarchar(100),
                           	@RequiredPermissionArray nvarchar(max)
                           )
                           AS
                           BEGIN
                               declare @rtQuery table (ViewpointTenantId int, ViewpointTenantName nvarchar(100), TopmostTenantId int, TopmostTenantName nvarchar(100), ChildTenantId int, ChildTenantName nvarchar(100), TenantUserId int, UserId nvarchar(100), ResultingChildRoleId int, ChildLevel int, TopmostParentLevel int)
                           insert into @rtQuery
                           exec GetDownwardsRoleTreeProc @pUserId=@userId, @pUserIsLabels=@UserIsLabels, @pViewPoint = @ViewpointTenantName
                           declare @permRaw table([value] nvarchar(150))
                           insert into @permRaw ([value]) select value from openjson(@RequiredPermissionArray) with ([value] nvarchar(150) '$')
                           
                           declare @perm2T table(permissionId int, tenantId int)
                           insert into @perm2T select p.PermissionId, r.ChildTenantId TenantId from
                           @rtQuery r
                           inner join RolePermissions rp on rp.RoleId = r.ResultingChildRoleId
                           inner join Permissions p on p.PermissionId = rp.PermissionId
                           inner join @permRaw rqr on p.PermissionName = rqr.value
                           union 
                           select p.PermissionId, r.ChildTenantId TenantId from
                           @rtQuery r
                           inner join GlobalToLocalRoles rp on rp.LocalRoleId = r.ResultingChildRoleId
                           inner join GlobalRolePermissions grp on grp.GlobalRoleId = rp.GlobalRoleId
                           inner join Permissions p on p.PermissionId = grp.PermissionId
                           inner join @permRaw rqr on p.PermissionName = rqr.value
                           
                           select t.TenantId, t.ParentTenantId, t.TenantName, t.DisplayName, null TenantPassword, t.TimeZone, t.TenantTypeId, t.TenantDirty 
                           from @perm2T r
                           inner join Tenants t on t.TenantId = r.tenantId
                           group by t.TenantId, t.ParentTenantId, t.TenantName, t.DisplayName, t.TimeZone, t.TenantTypeId, t.TenantDirty
                           END
                           """;
                var urtIdFunc = $$"""
                                  CREATE FUNCTION [{{schema}}].[GetUpwardsRoleTreeForId] 
                                  (
                                  	@UserId NVARCHAR(256),
                                  	@FromLeaf nvarchar(256)
                                  )
                                  RETURNS TABLE --(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(150), ParentTenantId int, ParentTenantName nvarchar(150), ParentRoleId int, ParentLevel int , TenantUserId int, UserId nvarchar(400), OutermostRoleId int)
                                  AS 
                                  return (
                                         with r as (select s.RoleId, s.RoleId ParentRoleId, s.TenantId, s.TenantId ParentTenantId, st.ParentTenantId as nextparent, s.RoleId as nextChildRole, level = 1 from SecurityRoles s inner join Tenants st on st.TenantId = s.TenantId
                                  union all
                                  select r_2.RoleId, pr.RoleId ParentRoleId, r_2.TenantId, pr.TenantId ParentTenantId, pt.ParentTenantId as nextparent, pr.RoleId as nextChildRole, r_2.level+1 level from r as r_2
                                  inner join RoleRoles roro on r_2.nextChildRole = roro.PermissiveRoleId
                                  inner join SecurityRoles pr on pr.RoleId = roro.PermittedRoleId and pr.TenantId =r_2.nextparent
                                  inner join Tenants ct on ct.TenantId = r_2.TenantId
                                  inner join Tenants pt on pt.TenantId = pr.TenantId and pr.TenantId = r_2.nextparent)
                                  
                                  
                                  select t.OutermostLeafTenantId, t.OutermostLeafTenantName, t.ParentTenantId, t.ParentTenantName, pr.RoleId ParentRoleId, t.ParentLevel, tu.TenantUserId, u.Id as UserId, cr.RoleId as OutermostRoleId from UpwardsTenantTree t
                                  inner join r on r.TenantId = t.OutermostLeafTenantId and r.ParentTenantId = t.ParentTenantId
                                  inner join TenantUsers tu on tu.TenantId = t.ParentTenantId
                                  inner join Users u on u.id = tu.UserId
                                  inner join SecurityRoles cr on cr.TenantId = r.TenantId and cr.RoleId = r.RoleId
                                  inner join SecurityRoles pr on pr.TenantId = r.ParentTenantId and pr.RoleId = r.ParentRoleId
                                  inner join TenantUserRoles tur on tur.TenantUserId = tu.TenantUserId and tur.RoleId = pr.RoleId
                                  where u.id = @userId  and (outermostleaftenantname = @FromLeaf or @FromLeaf is null)
                                  )
                                  """;
                var urtLblFunc = $$"""
                                   CREATE FUNCTION [{{schema}}].[GetUpwardsRoleTreeForLabels] 
                                   (
                                   	@UserId NVARCHAR(256),
                                   	@FromLeaf nvarchar(256)
                                   )
                                   RETURNS TABLE --(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(150), ParentTenantId int, ParentTenantName nvarchar(150), ParentRoleId int, ParentLevel int , TenantUserId int, UserId nvarchar(400), OutermostRoleId int)
                                   AS 
                                   return (
                                          with r as (select s.RoleId, s.RoleId ParentRoleId, s.TenantId, s.TenantId ParentTenantId, st.ParentTenantId as nextparent, s.RoleId as nextChildRole, level = 1 from SecurityRoles s inner join Tenants st on st.TenantId = s.TenantId
                                   union all
                                   select r_2.RoleId, pr.RoleId ParentRoleId, r_2.TenantId, pr.TenantId ParentTenantId, pt.ParentTenantId as nextparent, pr.RoleId as nextChildRole, r_2.level+1 level from r as r_2
                                   inner join RoleRoles roro on r_2.nextChildRole = roro.PermissiveRoleId
                                   inner join SecurityRoles pr on pr.RoleId = roro.PermittedRoleId and pr.TenantId =r_2.nextparent
                                   inner join Tenants ct on ct.TenantId = r_2.TenantId
                                   inner join Tenants pt on pt.TenantId = pr.TenantId and pr.TenantId = r_2.nextparent)
                                   
                                   
                                   select t.OutermostLeafTenantId, t.OutermostLeafTenantName, t.ParentTenantId, t.ParentTenantName, pr.RoleId ParentRoleId, t.ParentLevel, tu.TenantUserId, u.Id as UserId, cr.RoleId as OutermostRoleId from UpwardsTenantTree t
                                   inner join r on r.TenantId = t.OutermostLeafTenantId and r.ParentTenantId = t.ParentTenantId
                                   inner join TenantUsers tu on tu.TenantId = t.ParentTenantId
                                   inner join Users u on u.id = tu.UserId
                                   inner join openjson(@UserId) with ([value] nvarchar(150) '$') uta on u.NormalizedUserName = uta.value
                                   inner join SecurityRoles cr on cr.TenantId = r.TenantId and cr.RoleId = r.RoleId
                                   inner join SecurityRoles pr on pr.TenantId = r.ParentTenantId and pr.RoleId = r.ParentRoleId
                                   inner join TenantUserRoles tur on tur.TenantUserId = tu.TenantUserId and tur.RoleId = pr.RoleId
                                   where @FromLeaf is null or @FromLeaf = OutermostLeafTenantName
                                   )
                                   """;
                var drtTi = $$"""
             Create Procedure [{{schema}}].[GetDownwardsRoleTreeByVpIdProc] 
             (	
                	@pUserId NVARCHAR(256),
                	@puserIsLabels bit,
                	@pViewPointTenantId int
             )
             AS
             begin
                	declare @UserId nvarchar(256) = @pUserId
                	declare @userIsLabels bit = @puserIsLabels
                	declare @viewPointId int = @pViewPointTenantId
                	declare @completeUpTree table(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(100), ParentTenantId int, ParentTenantName nvarchar(100), ParentRoleId int, ParentLevel int , TenantUserId int, UserId nvarchar(100), OutermostRoleId int)
                	declare @completeUpTreeShrinker table(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(100), ParentLevel int, UserId nvarchar(100))
                	declare @resultingUpTree table(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(100), ParentTenantId int, ParentTenantName nvarchar(100), ParentRoleId int, ParentLevel int , TenantUserId int, UserId nvarchar(100), OutermostRoleId int)
                	if (@userIsLabels=1) begin
                   		insert into @completeUpTree select * from GetUpwardsRoleTreeForLabelsByLeafId(@userid, @viewPointId) --group by OutermostLeafTenantId, OutermostLeafTenantName, UserId
                	end else begin
                   		insert into @completeUpTree select * from GetUpwardsRoleTreeForIdByLeafId(@userid, @viewPointId) --group by OutermostLeafTenantId, OutermostLeafTenantName, UserId
                	end
                	insert into @completeUpTreeShrinker (OutermostLeafTenantId, OutermostLeafTenantName, UserId, ParentLevel) select OutermostLeafTenantId, OutermostLeafTenantName, UserId, min(parentlevel) as parentLevel from @completeUpTree group by OutermostLeafTenantId, OutermostLeafTenantName, UserId
                	insert into @resultingUpTree select b.* from @completeUpTreeShrinker a inner join @completeUpTree b on a.OutermostLeafTenantId = b.OutermostLeafTenantId and a.OutermostLeafTenantName = b.OutermostLeafTenantName and a.ParentLevel = b.ParentLevel and a.UserId = b.UserId
                	declare @rawtree table (
                   		TopmostTenantId int, 
                   		TopmostTenantName nvarchar(100), 
                   		TopmostRoleId int,
                   		ViewpointTenantId int, 
                   		ViewpointTenantName nvarchar(100), 
                   		ChildTenantId int, 
                   		ChildTenantName nvarchar(100), 
                   		ChildLevel int, 
                   		UserId nvarchar(100), 
                   		OutermostRoleId int
                	)
             
                	insert into @rawTree
                	SELECT   ParentTenantId TopmostTenantId, ParentTenantName TopmostTenantName, ParentRoleId TopMostRoleId, a.OutermostLeafTenantId ViewpointTenantId, a.OutermostLeafTenantName ViewpointTenantName, d.childtenantid, d.ChildTenantName, d.ChildLevel, UserId, OutermostRoleId--OutermostLeafTenantId, OutermostLeafTenantName, ParentTenantId, parenttenantname, parentlevel, tu.TenantUserId, u.id as UserId, OutermostRole OutermostRoleId
                	from @resultingUpTree a
                	inner join downwardstenanttree d on d.TopmostTenantId = OutermostLeafTenantId
                	;
             
                	with r as (select s.RoleId, s.RoleId ParentRoleId, s.TenantId, s.TenantId ParentTenantId, st.ParentTenantId as nextparent, s.RoleId as nextChildRole, level = 1 from SecurityRoles s inner join Tenants st on st.TenantId = s.TenantId
             union all
             select r_2.RoleId, pr.RoleId ParentRoleId, r_2.TenantId, pr.TenantId ParentTenantId, pt.ParentTenantId as nextparent, pr.RoleId as nextChildRole, r_2.level+1 level from r as r_2
             inner join RoleRoles roro on r_2.nextChildRole = roro.PermissiveRoleId
             inner join SecurityRoles pr on pr.RoleId = roro.PermittedRoleId and pr.TenantId =r_2.nextparent
             inner join Tenants ct on ct.TenantId = r_2.TenantId
             inner join Tenants pt on pt.TenantId = pr.TenantId and pr.TenantId = r_2.nextparent)
             
             
             SELECT   d.ViewpointTenantId, d.ViewpointTenantName, r.ParentTenantId TopmostTenantId, parenttenantname TopmostTenantName, OutermostLeafTenantId ChildTenantId, OutermostLeafTenantName ChildTenantName, tu.TenantUserId, u.id as UserId, r.RoleId ResultingChildRoleId, d.ChildLevel, parentlevel TopmostParentLevel from UpwardsTenantTree t
             inner join r on r.TenantId = t.OutermostLeafTenantId and r.ParentTenantId = t.ParentTenantId
             inner join TenantUsers tu on tu.TenantId = t.ParentTenantId
             inner join Users u on u.id = tu.UserId
             inner join SecurityRoles cr on cr.TenantId = r.TenantId and cr.RoleId = r.RoleId
             inner join SecurityRoles pr on pr.TenantId = r.ParentTenantId and pr.RoleId = r.ParentRoleId
             inner join TenantUserRoles tur on tur.TenantUserId = tu.TenantUserId and tur.RoleId = pr.RoleId
             inner join @rawtree d on d.TopmostTenantId = r.ParentTenantId and d.ChildTenantId = OutermostLeafTenantId and d.UserId = u.Id and d.TopmostRoleId = pr.RoleId
             
             end
             """;
                var ctwpTi = $$"""
           CREATE procedure [{{schema}}].[GetChildTenantsWithPermsByVpIdProc]
           (
               @UserId nvarchar(100),
              	@UserIsLabels bit,
              	@ViewPointTenantId int,
              	@RequiredPermissionArray nvarchar(max)
           )
           AS
           BEGIN
               declare @rtQuery table (ViewpointTenantId int, ViewpointTenantName nvarchar(100), TopmostTenantId int, TopmostTenantName nvarchar(100), ChildTenantId int, ChildTenantName nvarchar(100), TenantUserId int, UserId nvarchar(100), ResultingChildRoleId int, ChildLevel int, TopmostParentLevel int)
           insert into @rtQuery
           exec GetDownwardsRoleTreeByVpIdProc @pUserId=@userId, @pUserIsLabels=@UserIsLabels, @pViewPointTenantId = @ViewpointTenantId
           declare @permRaw table([value] nvarchar(150))
           insert into @permRaw ([value]) select value from openjson(@RequiredPermissionArray) with ([value] nvarchar(150) '$')
           
           declare @perm2T table(permissionId int, tenantId int)
           insert into @perm2T select p.PermissionId, r.ChildTenantId TenantId from
           @rtQuery r
           inner join RolePermissions rp on rp.RoleId = r.ResultingChildRoleId
           inner join Permissions p on p.PermissionId = rp.PermissionId
           inner join @permRaw rqr on p.PermissionName = rqr.value
           union 
           select p.PermissionId, r.ChildTenantId TenantId from
           @rtQuery r
           inner join GlobalToLocalRoles rp on rp.LocalRoleId = r.ResultingChildRoleId
           inner join GlobalRolePermissions grp on grp.GlobalRoleId = rp.GlobalRoleId
           inner join Permissions p on p.PermissionId = grp.PermissionId
           inner join @permRaw rqr on p.PermissionName = rqr.value
           
           select t.TenantId, t.ParentTenantId, t.TenantName, t.DisplayName, null TenantPassword, t.TimeZone, t.TenantTypeId, t.TenantDirty 
           from @perm2T r
           inner join Tenants t on t.TenantId = r.tenantId
           group by t.TenantId, t.ParentTenantId, t.TenantName, t.DisplayName, t.TimeZone, t.TenantTypeId, t.TenantDirty
           END
           """;

                var urtIdFuncTi = $$"""
                  CREATE FUNCTION [{{schema}}].[GetUpwardsRoleTreeForIdByLeafId] 
                  (
                     	@UserId NVARCHAR(256),
                     	@FromLeafTenantId int
                  )
                  RETURNS TABLE --(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(150), ParentTenantId int, ParentTenantName nvarchar(150), ParentRoleId int, ParentLevel int , TenantUserId int, UserId nvarchar(400), OutermostRoleId int)
                  AS 
                  return (
                         with r as (select s.RoleId, s.RoleId ParentRoleId, s.TenantId, s.TenantId ParentTenantId, st.ParentTenantId as nextparent, s.RoleId as nextChildRole, level = 1 from SecurityRoles s inner join Tenants st on st.TenantId = s.TenantId
                  union all
                  select r_2.RoleId, pr.RoleId ParentRoleId, r_2.TenantId, pr.TenantId ParentTenantId, pt.ParentTenantId as nextparent, pr.RoleId as nextChildRole, r_2.level+1 level from r as r_2
                  inner join RoleRoles roro on r_2.nextChildRole = roro.PermissiveRoleId
                  inner join SecurityRoles pr on pr.RoleId = roro.PermittedRoleId and pr.TenantId =r_2.nextparent
                  inner join Tenants ct on ct.TenantId = r_2.TenantId
                  inner join Tenants pt on pt.TenantId = pr.TenantId and pr.TenantId = r_2.nextparent)
                  
                  
                  select t.OutermostLeafTenantId, t.OutermostLeafTenantName, t.ParentTenantId, t.ParentTenantName, pr.RoleId ParentRoleId, t.ParentLevel, tu.TenantUserId, u.Id as UserId, cr.RoleId as OutermostRoleId from UpwardsTenantTree t
                  inner join r on r.TenantId = t.OutermostLeafTenantId and r.ParentTenantId = t.ParentTenantId
                  inner join TenantUsers tu on tu.TenantId = t.ParentTenantId
                  inner join Users u on u.id = tu.UserId
                  inner join SecurityRoles cr on cr.TenantId = r.TenantId and cr.RoleId = r.RoleId
                  inner join SecurityRoles pr on pr.TenantId = r.ParentTenantId and pr.RoleId = r.ParentRoleId
                  inner join TenantUserRoles tur on tur.TenantUserId = tu.TenantUserId and tur.RoleId = pr.RoleId
                  where u.id = @userId  and (OutermostLeafTenantId = @FromLeafTenantId or @FromLeafTenantId is null)
                  )
                  """;

                var urtLblFuncTi = $$"""
                   CREATE FUNCTION [{{schema}}].[GetUpwardsRoleTreeForLabelsByLeafId] 
                   (
                      	@UserId NVARCHAR(256),
                      	@FromLeafTenantId int
                   )
                   RETURNS TABLE --(OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(150), ParentTenantId int, ParentTenantName nvarchar(150), ParentRoleId int, ParentLevel int , TenantUserId int, UserId nvarchar(400), OutermostRoleId int)
                   AS 
                   return (
                          with r as (select s.RoleId, s.RoleId ParentRoleId, s.TenantId, s.TenantId ParentTenantId, st.ParentTenantId as nextparent, s.RoleId as nextChildRole, level = 1 from SecurityRoles s inner join Tenants st on st.TenantId = s.TenantId
                   union all
                   select r_2.RoleId, pr.RoleId ParentRoleId, r_2.TenantId, pr.TenantId ParentTenantId, pt.ParentTenantId as nextparent, pr.RoleId as nextChildRole, r_2.level+1 level from r as r_2
                   inner join RoleRoles roro on r_2.nextChildRole = roro.PermissiveRoleId
                   inner join SecurityRoles pr on pr.RoleId = roro.PermittedRoleId and pr.TenantId =r_2.nextparent
                   inner join Tenants ct on ct.TenantId = r_2.TenantId
                   inner join Tenants pt on pt.TenantId = pr.TenantId and pr.TenantId = r_2.nextparent)
                   
                   
                   select t.OutermostLeafTenantId, t.OutermostLeafTenantName, t.ParentTenantId, t.ParentTenantName, pr.RoleId ParentRoleId, t.ParentLevel, tu.TenantUserId, u.Id as UserId, cr.RoleId as OutermostRoleId from UpwardsTenantTree t
                   inner join r on r.TenantId = t.OutermostLeafTenantId and r.ParentTenantId = t.ParentTenantId
                   inner join TenantUsers tu on tu.TenantId = t.ParentTenantId
                   inner join Users u on u.id = tu.UserId
                   inner join openjson(@UserId) with ([value] nvarchar(150) '$') uta on u.NormalizedUserName = uta.value
                   inner join SecurityRoles cr on cr.TenantId = r.TenantId and cr.RoleId = r.RoleId
                   inner join SecurityRoles pr on pr.TenantId = r.ParentTenantId and pr.RoleId = r.ParentRoleId
                   inner join TenantUserRoles tur on tur.TenantUserId = tu.TenantUserId and tur.RoleId = pr.RoleId
                   where @FromLeafTenantId is null or @FromLeafTenantId = OutermostLeafTenantId
                   )
                   """;
                /*var urt = $$"""
                             CREATE FUNCTION [{{schema}}].[GetUpwardsRoleTree] 
                             (
                             	@UserId NVARCHAR(256),
                             	@userIsLabels bit,
                             	@FromLeaf nvarchar(256)
                             )
                             RETURNS @return TABLE (OutermostLeafTenantId int, OutermostLeafTenantName nvarchar(150), ParentTenantId int, ParentTenantName nvarchar(150), ParentRoleId int, ParentLevel int , TenantUserId int, UserId nvarchar(400), OutermostRoleId int)
                             AS begin
                             	if (@userIsLabels = 0) begin
                             insert into @return 
                             select * from [GetUpwardsRoleTreeForId](@UserId, @FromLeaf)
                                end else begin
                             insert into @return 
                             select * from GetUpwardsRoleTreeForLabels(@UserId, @FromLeaf)
                                end
                                return
                             end
                             """;*/
                migrationBuilder.Sql(urtIdFunc);
                migrationBuilder.Sql(urtLblFunc);
                //migrationBuilder.Sql(urt);
                migrationBuilder.Sql(drt);
                migrationBuilder.Sql(ctwp);
                migrationBuilder.Sql(urtIdFuncTi);
                migrationBuilder.Sql(urtLblFuncTi);
                //migrationBuilder.Sql(urt);
                migrationBuilder.Sql(drtTi);
                migrationBuilder.Sql(ctwpTi);
            }
        }

        private static IQueryable<HierarchyTenantUser> GetRawUserQuery(TargetInterface c, string userId, string currentTenant, out IList<DownwardsUserRoleView<string>> fullUserList)
        {
            fullUserList = c.GetDownwardsTenantUserRoles(userId, false, currentTenant).ToList();
            var phase1 = (from t in c.TenantUsers
                join j in fullUserList.Select(n => n.TenantUserId).AsQueryable() on t.TenantUserId equals j
                select t);
            return phase1;
        }
    }
}

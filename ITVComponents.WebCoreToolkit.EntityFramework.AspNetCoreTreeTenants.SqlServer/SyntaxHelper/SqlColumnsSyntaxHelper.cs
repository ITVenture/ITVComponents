using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using TargetInterface = ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.IHierarchySecurityContext<ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.HierarchyTenant, string, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.User, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.Role, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.Permission, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.UserRole, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.RolePermission,
    ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.HierarchyTenantUser, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.RoleRole, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.NavigationMenu, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.TenantNavigationMenu, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.DiagnosticsQuery,
    ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.DiagnosticsQueryParameter, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.TenantDiagnosticsQuery, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.DashboardWidget, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.DashboardParam,
    ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.DashboardWidgetLocalization, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.UserWidget
    , ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.CustomUserProperty, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.AssetTemplate, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.AssetTemplatePath, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.AssetTemplateGrant, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.AssetTemplateFeature,
    ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.SharedAsset, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.SharedAssetUserFilter, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.SharedAssetTenantFilter, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.ClientAppTemplate, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.AppPermission,
    ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.AppPermissionSet, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.ClientAppTemplatePermission, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.ClientApp, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.ClientAppPermission, ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.ClientAppUser,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels.HierarchyWebPlugin, ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels.HierarchyWebPluginConstant, ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels.HierarchyWebPluginGenericParameter,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels.HierarchySequence, ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels.HierarchyTenantSetting, ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels.HierarchyTenantFeatureActivation,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models.HierarchyTenantContextSecurityTrustConfig>;
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
            ConfigureVirtualTables(builderOptions);
        }

        public static void ConfigureVirtualTables(IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureEntity<UpwardsTenantView>(uu =>
                uu.ToTable(GlobalDbObjectNaming.UpwardsTenantTreeView, b => b.ExcludeFromMigrations()).HasNoKey());
            builderOptions.ConfigureEntity<DownwardsTenantView>(dd =>
                dd.ToTable(GlobalDbObjectNaming.DownwardsTenantTreeView, b => b.ExcludeFromMigrations()).HasNoKey());
            builderOptions.ConfigureEntity<UpwardsRoleUserView<string>>(pp =>
                pp.ToTable(GlobalDbObjectNaming.UpwardsRoleTreeView, b => b.ExcludeFromMigrations()).HasNoKey());
            builderOptions.ConfigureEntity<DownwardsUserRoleView<string>>(puv =>
                puv.ToTable(GlobalDbObjectNaming.DownwardsRoleTreeView, b => b.ExcludeFromMigrations())
                    .HasNoKey());
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
            bld.ConfigureMethod(GlobalDbObjectNaming.ChildTenantsWithProc,
                (DbContext c, string userId, string currentTenant, string[] requiredPermissions) =>
                {
                    var ctx = (TargetInterface)c;
                    var tmpUserQuery = GetRawUserQuery(ctx, currentTenant);
                    var tmpRt = (from t in tmpUserQuery
                            join rp in ctx.RolePermissions on new { t.TenantId, t.RoleId } equals new
                                { rp.TenantId, rp.RoleId }
                            join p in ctx.Permissions on rp.PermissionId equals p.PermissionId
                            where requiredPermissions.Contains(p.PermissionName) && t.User.Id == userId
                            select new { t.TenantId, t.User.Id }
                        );
                    return (from t in ctx.Tenants
                        join r in tmpRt on t.TenantId equals r.TenantId
                        select t).Distinct();
                    /*var tmpRet = c.Set<DownwardsUserRoleView<string>>()
                        .Join(c.Set<HierarchyTenant>(), l => l.ChildTenantId, r => r.TenantId,
                            (l, r) => new { Left = l, Right = r })
                        .Where(n => n.Left.UserId == userId && n.Left.ViewpointTenantName == currentTenant)
                        .GroupBy(n => new
                        {
                            n.Left.UserId, n.Left.TopmostParentLevel, n.Left.ChildTenantName, n.Left.ChildTenantId,
                            n.Right
                        })
                        .OrderBy(g => g.Key.ChildTenantName).ThenBy(g => g.Key.TopmostParentLevel)
                        .Select(g => new
                        {
                            g.Key.ChildTenantName, g.Key.ChildTenantId, g.Key.UserId,
                            HasRequiredPermissions = g.Any(p => requiredPermissions.Contains(p.Left.PermissionName)),
                            Tenant = g.Key.Right
                        });
                    var st = new List<string>();
                    var retVal = new List<HierarchyTenant>();
                    foreach (var tmp in tmpRet)
                    {
                        var r = !st.Contains(tmp.ChildTenantName);
                        if (!r)
                        {
                            st.Add(tmp.ChildTenantName);
                        }

                        if (r && tmp.HasRequiredPermissions)
                        {
                            retVal.Add(tmp.Tenant);
                        }
                    }

                    return retVal.AsQueryable();*/
                });
        }

        public static void ConfigureViews(MigrationBuilder migrationBuilder, string schema = "dbo", bool dropFirst = false)
        {
            if (dropFirst)
            {
                migrationBuilder.Sql($@"DROP VIEW [{schema}].[UpwardsTenantTree]");
                migrationBuilder.Sql($@"DROP VIEW [{schema}].[DownwardsTenantTree]");
                migrationBuilder.Sql($@"DROP VIEW [{schema}].[UpwardsRoleTree]");
                migrationBuilder.Sql($@"DROP VIEW [{schema}].[DownwardsRoleTree]");

            }
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
inner join users u on u.id = tu.UserId
inner join rolepermissions trp on trp.roleid = cr.RoleId");

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

SELECT   TopmostTenantId ViewPointTenantId, TopmostTenantName ViewPointTenantName, ChildTenantId, ChildTenantName, ChildLevel, r.TenantUserId,
cr.roleid ResultingChildRoleId, r.ParentTenantId TopmostTenantId, r.parentTenantName TopmostTenantName, r.ParentLevel as TopmostParentLevel
FROM         z AS z_1  
inner join [UpwardsRoleTree] r
on z_1.TopmostTenantId = r.OutermostLeafTenantId and z_1.topmostRoleId = r.OutermostRoleId
inner join securityroles pr on pr.roleid = r.OutermostRoleId
inner join securityroles cr on cr.roleid = z_1.currentrole
inner join tenantuserroles tur on tur.roleid in (pr.roleid, cr.RoleId)
inner join tenantusers tu on tu.tenantuserid = tur.TenantUserId and tu.tenantuserid = r.tenantuserid
inner join users u on u.id = tu.UserId
inner join rolepermissions trp on trp.roleid = cr.RoleId and trp.tenantid = Z_1.ChildTenantId");
        }

        private static IQueryable<UserTenantLevel<User>> GetRawUserQuery(TargetInterface c, string currentTenant)
        {
            var phase1 = (from t in c.TenantUsers
                join j in c.DownwardsTenantUserRoles on t.TenantUserId equals j.TenantUserId
                where j.ViewpointTenantName == currentTenant
                select new { t.UserId, t.User, j.ChildLevel, ParentLevel = j.TopmostParentLevel, TenantId = j.ViewPointTenantId });
            var phase2 = (from gj in phase1
                group gj by new {gj.UserId, gj.TenantId}
                into g
                select new
                {
                    TenantId = g.Key.TenantId,
                    UserId = g.Key.UserId,
                    Level = g.Min(us => us.ParentLevel)
                });
            return (from p in phase2
                join t in c.DownwardsTenantUserRoles on new { p.Level, p.UserId, p.TenantId } equals new
                    { Level = t.TopmostParentLevel, t.UserId, TenantId = t.ViewPointTenantId }
                join tn in c.TenantUsers on t.TenantUserId equals tn.TenantUserId
                select new UserTenantLevel<User> { User = tn.User, Level = t.ChildLevel, TenantId = t.ChildTenantId, RoleId = t.ResultingChildRoleId });
        }
    }
}

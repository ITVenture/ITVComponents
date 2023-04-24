using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.SqlServer.SyntaxHelper
{
    public static class SqlColumnsSyntaxHelper
    {
        public static void ConfigureComputedColumns<TContext>(DbContextModelBuilderOptions<TContext> builderOptions)
        {
            builderOptions.ConfigureComputedColumn<NavigationMenu, string>(n => n.UrlUniqueness, "case when isnull(Url,'')='' and isnull(RefTag,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) when isnull(Url,'')='' then RefTag else Url end persisted");
            builderOptions.ConfigureComputedColumn<Role,string>(r => r.RoleNameUniqueness, "'__T'+convert(varchar(10),TenantId)+'##'+RoleName persisted");
            builderOptions.ConfigureComputedColumn<Permission, string>(p => p.PermissionNameUniqueness, "case when TenantId is null then PermissionName else '__T'+convert(varchar(10),TenantId)+'##'+PermissionName end persisted");
            builderOptions.ConfigureComputedColumn<WebPlugin, string>(w => w.PluginNameUniqueness, "case when TenantId is null then UniqueName else '__T'+convert(varchar(10),TenantId)+'##'+UniqueName end persisted");
            builderOptions.ConfigureComputedColumn<WebPluginConstant, string>(c => c.NameUniqueness, "case when TenantId is null then Name else '__T'+convert(varchar(10),TenantId)+'##'+Name end persisted");
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
                return tmp.First();
            });
        }
    }
}

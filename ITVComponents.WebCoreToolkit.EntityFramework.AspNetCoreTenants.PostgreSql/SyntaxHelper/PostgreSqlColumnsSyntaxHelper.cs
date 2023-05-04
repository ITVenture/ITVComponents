using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using Npgsql;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.PostgreSql.SyntaxHelper
{
    public static class PostgreSqlColumnsSyntaxHelper
    {
        public static void ConfigureComputedColumns<TContext>(DbContextModelBuilderOptions<TContext> builderOptions)
        {
            builderOptions.ConfigureComputedColumn<NavigationMenu, string>(n => n.UrlUniqueness, "case when COALESCE(\"Url\",'')='' and COALESCE(\"RefTag\",'')='' then 'MENU__'||cast(\"NavigationMenuId\" as character varying(10)) when COALESCE(\"Url\",'')='' then \"RefTag\" else \"Url\" end");
            builderOptions.ConfigureComputedColumn<Role, string>(r => r.RoleNameUniqueness, "'__T'||cast(\"TenantId\" as character varying(10))||'##'||\"RoleName\"");
            builderOptions.ConfigureComputedColumn<Permission, string>(p => p.PermissionNameUniqueness, "case when \"TenantId\" is null then \"PermissionName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"PermissionName\" end");
            builderOptions.ConfigureComputedColumn<WebPlugin, string>(w => w.PluginNameUniqueness, "case when \"TenantId\" is null then \"UniqueName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"UniqueName\" end");
            builderOptions.ConfigureComputedColumn<WebPluginConstant, string>(c => c.NameUniqueness, "case when \"TenantId\" is null then \"Name\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"Name\" end");
        }

        public static void ConfigureMethods(IContextModelBuilderOptions bld)
        {
            bld.ConfigureMethod("SequenceNextVal", (DbContext c, string name, int tenantId) =>
            {
                var tmp = c.Database.SqlQuery<ValueTableModel<int>>(@"UPDATE ""Sequences""
    SET ""CurrentValue"" = case when ""CurrentValue""+""StepSize""<=""MaxValue"" then ""CurrentValue""+""StepSize"" when ""CurrentValue""+""StepSize"" > ""MaxValue"" and ""Cycle""=true then ""MinValue"" else -1 end
    WHERE ""SequenceName""= @name and ""TenantId"" = @tenantId
    RETURNING ""CurrentValue"" ""Value""", new NpgsqlParameter("name", name),
                    new NpgsqlParameter("tenantId", tenantId));
                return tmp.First().Value;
            });
        }
    }
}

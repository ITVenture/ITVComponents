﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.SqlServer.SyntaxHelper
{
    public static class SqlColumnsSyntaxHelper
    {
        public static void ConfigureComputedColumns<TContext>(DbContextModelBuilderOptions<TContext> builderOptions)
        {
            builderOptions.ConfigureComputedColumn<NavigationMenu, string>(n => n.UrlUniqueness, "case when isnull(Url,'')='' and isnull(RefTag,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) when isnull(Url,'')='' then RefTag else Url end persisted");
            builderOptions.ConfigureComputedColumn<Role, string>(r => r.RoleNameUniqueness, "'__T'+convert(varchar(10),TenantId)+'##'+RoleName persisted");
            builderOptions.ConfigureComputedColumn<Permission, string>(p => p.PermissionNameUniqueness, "case when TenantId is null then PermissionName else '__T'+convert(varchar(10),TenantId)+'##'+PermissionName end persisted");
            builderOptions.ConfigureComputedColumn<WebPlugin, string>(w => w.PluginNameUniqueness, "case when TenantId is null then UniqueName else '__T'+convert(varchar(10),TenantId)+'##'+UniqueName end persisted");
            builderOptions.ConfigureComputedColumn<WebPluginConstant, string>(c => c.NameUniqueness, "case when TenantId is null then Name else '__T'+convert(varchar(10),TenantId)+'##'+Name end persisted");
        }
    }
}

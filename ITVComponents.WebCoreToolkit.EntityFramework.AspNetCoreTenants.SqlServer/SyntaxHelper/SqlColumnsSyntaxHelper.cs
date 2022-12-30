using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.SyntaxHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.SqlServer.SyntaxHelper
{
    public class SqlColumnsSyntaxHelper: ICalculatedColumnsSyntaxProvider
    {
        private ConcurrentDictionary<Type, Dictionary<string, string>> knownColumns = new();

        public SqlColumnsSyntaxHelper()
        {
            RegisterCalculatedColumn<NavigationMenu>(n => n.UrlUniqueness, "case when isnull(Url,'')='' and isnull(RefTag,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) when isnull(Url,'')='' then RefTag else Url end persisted");
            RegisterCalculatedColumn<Role>(r => r.RoleNameUniqueness, "'__T'+convert(varchar(10),TenantId)+'##'+RoleName persisted");
            RegisterCalculatedColumn<Permission>(p => p.PermissionNameUniqueness, "case when TenantId is null then PermissionName else '__T'+convert(varchar(10),TenantId)+'##'+PermissionName end persisted");
            RegisterCalculatedColumn<WebPlugin>(w => w.PluginNameUniqueness, "case when TenantId is null then UniqueName else '__T'+convert(varchar(10),TenantId)+'##'+UniqueName end persisted");
            RegisterCalculatedColumn<WebPluginConstant>(c => c.NameUniqueness, "case when TenantId is null then Name else '__T'+convert(varchar(10),TenantId)+'##'+Name end persisted");
        }

        //
        public PropertyBuilder<T> WithCalculatedPropert<T>(PropertyBuilder<T> property)
        {
            var dc = GetEntity(property.Metadata.DeclaringEntityType.ClrType);
            Console.WriteLine(property.Metadata.Name);
            if (dc.TryGetValue(property.Metadata.Name, out var ret))
            {
                return property.HasComputedColumnSql(ret, true);
            }

            return null;
        }

        public void RegisterCalculatedColumn<T>(Expression<Func<T, object>> name, string calculatedCommand)
        {
            var dc = GetEntity(typeof(T));
            dc[name.GetPropertyAccess().Name] = calculatedCommand;
        }

        private Dictionary<string, string> GetEntity(Type t)
        {
            return knownColumns.GetOrAdd(t, t => new Dictionary<string, string>());
        }
    }
}

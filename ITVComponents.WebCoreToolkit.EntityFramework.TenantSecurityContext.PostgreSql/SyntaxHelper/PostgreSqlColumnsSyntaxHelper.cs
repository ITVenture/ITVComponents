using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.SyntaxHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.PostgreSql.SyntaxHelper
{
    public class PostgreSqlColumnsSyntaxHelper: ICalculatedColumnsSyntaxProvider
    {
        private ConcurrentDictionary<Type, Dictionary<string, string>> knownColumns = new();

        public PostgreSqlColumnsSyntaxHelper()
        {
            RegisterCalculatedColumn<NavigationMenu>(n => n.UrlUniqueness, "case when COALESCE(\"Url\",'')='' and COALESCE(\"RefTag\",'')='' then 'MENU__'||cast(\"NavigationMenuId\" as character varying(10)) when COALESCE(\"Url\",'')='' then \"RefTag\" else \"Url\" end");
            RegisterCalculatedColumn<Role>(r => r.RoleNameUniqueness, "'__T'||cast(\"TenantId\" as character varying(10))||'##'||\"RoleName\"");
            RegisterCalculatedColumn<Permission>(p => p.PermissionNameUniqueness, "case when \"TenantId\" is null then \"PermissionName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"PermissionName\" end");
            RegisterCalculatedColumn<WebPlugin>(w => w.PluginNameUniqueness, "case when \"TenantId\" is null then \"UniqueName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"UniqueName\" end");
            RegisterCalculatedColumn<WebPluginConstant>(c => c.NameUniqueness, "case when \"TenantId\" is null then \"Name\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"Name\" end");
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

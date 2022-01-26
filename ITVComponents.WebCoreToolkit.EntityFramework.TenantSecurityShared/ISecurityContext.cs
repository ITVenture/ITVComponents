using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.EntityFrameworkCore;
using SystemEvent = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.SystemEvent;
using WebPlugin = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.WebPlugin;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared
{
    [ExplicitlyExpose]
    public interface ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty>:IBaseTenantContext
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TNavigationMenu : NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TWidgetParam : DashboardParam<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserWidget: UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserProperty:CustomUserProperty<TUserId,TUser>
        where TUser : class
    {

        public DbSet<TUser> Users { get; set; }

        public DbSet<TWidget> Widgets { get; set; }

        public DbSet<TWidgetParam> WidgetParams { get; set; }

        public DbSet<TUserWidget> UserWidgets { get; set; }

        public DbSet<TUserProperty> UserProperties { get; set; }

        public DbSet<TRole> Roles { get; set; }

        public DbSet<TPermission> Permissions { get; set; }

        public DbSet<TUserRole> UserRoles { get; set; }

        public DbSet<TRolePermission> RolePermissions { get; set; }

        public DbSet<TTenantUser> TenantUsers { get; set; }

        public DbSet<TNavigationMenu> Navigation { get; set; }

        public DbSet<TTenantNavigation> TenantNavigation { get; set; }

        public DbSet<TQuery> DiagnosticsQueries { get; set; }

        public DbSet<TQueryParameter> DiagnosticsQueryParameters { get; set; }

        public DbSet<TTenantQuery> TenantDiagnosticsQueries { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security;
using ITVComponents.Formatting;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using CustomUserProperty = ITVComponents.WebCoreToolkit.Models.CustomUserProperty;
using Permission = ITVComponents.WebCoreToolkit.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.Models.Role;
using User = ITVComponents.WebCoreToolkit.Models.User;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Security
{
    public abstract class DbSecurityRepository<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty> : ISecurityRepository
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
        where TUserWidget : UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
    {
        private readonly ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty> securityContext;

        protected DbSecurityRepository(ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty> securityContext)
        {
            this.securityContext = securityContext;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a list of users in the current application
        /// </summary>
        public ICollection<User> Users
        {
            get
            {
                using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
                return (from u in securityContext.Users
                    select SelectUser(u)).ToList();
            }
        }

        /// <summary>
        /// Gets a list of Roles that can be granted to users in the current application
        /// </summary>
        public ICollection<Role> Roles
        {
            get
            {
                using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
                return (from r in securityContext.SecurityRoles select r).ToList<Role>();
            }
        }

        /// <summary>
        /// Gets a collection of defined Permissions in the current application
        /// </summary>
        public ICollection<Permission> Permissions
        {
            get
            {
                using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
                return (from p in securityContext.Permissions select p).ToList<Permission>();
            }
        }

        /// <summary>
        /// Gets an enumeration of Roles that are assigned to the given user
        /// </summary>
        /// <param name="user">the user for which to get the roles</param>
        /// <returns>an enumerable of all the user-roles</returns>
        public IEnumerable<Role> GetRoles(User user)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role).ToArray();
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for the given user
        /// </summary>
        /// <param name="user">the user for which to get the custom properties</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public IEnumerable<CustomUserProperty> GetCustomProperties(User user)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return (from p in UserProps(securityContext.Users.First(UserFilter(user))) select p).ToArray();

        }

        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
        {
            var t = securityContext.CurrentTenantId;
            if (t != null)
            {
                var ti = t.Value;
                var tenantUsers = securityContext.TenantUsers.Where(tu => tu.TenantId == ti).Select(u => u.User);
                return tenantUsers.Any(UserFilter(userLabels, userAuthenticationType));
            }

            return false;
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return (from u in securityContext.Users.Where(UserFilter(userLabels,userAuthenticationType))
                    .Join(securityContext.UserProperties, UserId, p => p.UserId, (tu,tp) => tp)
                select u).ToArray();
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="originalClaims">the claims that were originally attached to the current identity</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims,
            string userAuthenticationType)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            var typeClaims = securityContext.AuthenticationClaimMappings.Where(n =>
                n.AuthenticationType.AuthenticationTypeName == userAuthenticationType).ToArray();
            var preMapped = from t in originalClaims
                join i in typeClaims on t.Type equals i.IncomingClaimName
                select new { Original = t, Map = i };
            return (from t in preMapped where string.IsNullOrEmpty(t.Map.Condition) || (ExpressionParser.Parse(t.Map.Condition, t.Original) is bool b && b)
                   select TryGetClaim(t.Map,t.Original)).Where(n => n != null);
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given user through its roles
        /// </summary>
        /// <param name="user">the user for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given user</returns>
        public IEnumerable<Permission> GetPermissions(User user)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return (from p in (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role.RolePermissions).SelectMany(rp => rp) select new Permission
            {
                //PermissionName = $"{(!p.Permission.IsGlobal?p.Tenant.TenantName:"")}{p.Permission.PermissionName}"
                PermissionName = p.Permission.PermissionName
            }).Distinct().ToArray();
        }

        /// <summary>
        /// Gets an enumeration of Permissions for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of permissions for the given user-labels</returns>
        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return (from tr in securityContext.Users.Where(UserFilter(userLabels,userAuthenticationType))
                    .Join(securityContext.TenantUsers, UserId, tr => tr.UserId, (tu,tt) => tt)
                join ur in securityContext.TenantUserRoles on tr.TenantUserId equals ur.TenantUserId
                    join r in securityContext.SecurityRoles on new {ur.RoleId, tr.TenantId} equals new { r.RoleId, r.TenantId }
                    join rp in securityContext.RolePermissions on new {r.RoleId, r.TenantId} equals new {rp.RoleId, rp.TenantId}
                    join rt in securityContext.Tenants on rp.TenantId equals rt.TenantId
                    join p in securityContext.Permissions on rp.PermissionId equals p.PermissionId
                    select new Permission
                    {
                        //PermissionName = p.PermissionName != rt.TenantName?$"{(!p.IsGlobal?rt.TenantName:"")}{p.PermissionName}":p.PermissionName
                        PermissionName = p.PermissionName
                    }).Distinct().ToArray();
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given Role
        /// </summary>
        /// <param name="role">the role for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given role</returns>
        public IEnumerable<Permission> GetPermissions(Role role)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            if (role is TRole dbRole)
            {
                return from p in dbRole.RolePermissions select p.Permission;
            }

            return (from p in securityContext.SecurityRoles.First(r => r.RoleName == role.RoleName).RolePermissions select new Permission
            {
                //PermissionName = $"{(!p.Permission.IsGlobal ? p.Tenant.TenantName : "")}{p.Permission.PermissionName}"
                PermissionName = p.Permission.PermissionName
            }).ToArray();
        }

        /// <summary>
        /// Gets a value indicating whether the specified Permission-Scope exists
        /// </summary>
        /// <param name="permissionScopeName">the permissionScope to check for existence</param>
        /// <returns>a value indicating whether the specified permissionScope is valid</returns>
        public bool PermissionScopeExists(string permissionScopeName)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return securityContext.Tenants.Any(n => n.TenantName == permissionScopeName);
        }

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string authType)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, true, false);
            return (from d in (from t in securityContext.Users.Where(UserFilter(userLabels, authType))
                        .Join(securityContext.TenantUsers, UserId, u => u.UserId, (tu, tt) => tt.Tenant)
                    select t).Distinct()
                orderby d.DisplayName
                select new ScopeInfo { ScopeDisplayName = d.DisplayName, ScopeName = d.TenantName }).ToArray();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        protected abstract User SelectUser(TUser src);

        protected abstract System.Linq.Expressions.Expression<Func<TUser, bool>> UserFilter(User user);

        protected abstract System.Linq.Expressions.Expression<Func<TUser, bool>> UserFilter(string[] userLabels, string authType);

        protected abstract IEnumerable<TUserRole> AllRoles(TUser user);

        protected abstract IEnumerable<CustomUserProperty<TUserId, TUser>> UserProps(TUser user);

        protected abstract Expression<Func<TUser, TUserId>> UserId { get;}

        /// <summary>
        /// raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Estimats a claimData item from a given mapping and an original claim
        /// </summary>
        /// <param name="map">the mapping instruction for estimating a new claim</param>
        /// <param name="original">the original claim-value</param>
        /// <returns>a new claim that must be added to the currently logged on user</returns>
        private ClaimData TryGetClaim(AuthenticationClaimMapping map, ClaimData original)
        {
            try
            {
                return new ClaimData
                {
                    Type = original.FormatText(map.OutgoingClaimName),
                    ValueType = !string.IsNullOrEmpty(map.OutgoingValueType) ? original.FormatText(map.OutgoingValueType) : "",
                    Issuer = !string.IsNullOrEmpty(map.OutgoingIssuer) ? original.FormatText(map.OutgoingIssuer) : "",
                    OriginalIssuer = !string.IsNullOrEmpty(map.OutgoingOriginalIssuer) ? original.FormatText(map.OutgoingOriginalIssuer) : "",
                    Value = !string.IsNullOrEmpty(map.OutgoingClaimValue) ? original.FormatText(map.OutgoingClaimValue) : ""
                };
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}

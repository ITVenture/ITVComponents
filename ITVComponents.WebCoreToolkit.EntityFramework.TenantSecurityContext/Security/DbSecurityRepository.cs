using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Security
{
    internal class DbSecurityRepository:ISecurityRepository
    {
        private readonly SecurityContext securityContext;

        public DbSecurityRepository(SecurityContext securityContext)
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
                    select new User
                    {
                        UserName = u.UserName,
                        AuthenticationType = u.AuthenticationType.AuthenticationTypeName
                    }).ToList();
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
                return (from r in securityContext.Roles select r).ToList<Role>();
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
            return (from r in securityContext.Users.First((System.Linq.Expressions.Expression<Func<Models.User, bool>>)(n => n.UserName == user.UserName && n.AuthenticationType.AuthenticationTypeName == user.AuthenticationType)).TenantUsers.SelectMany(u => u.Roles) select r.Role).ToArray();
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for the given user
        /// </summary>
        /// <param name="user">the user for which to get the custom properties</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public IEnumerable<CustomUserProperty> GetCustomProperties(User user)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return (from p in securityContext.Users.First(n => n.UserName == user.UserName && n.AuthenticationType.AuthenticationTypeName == user.AuthenticationType).UserProperties select p).ToArray();

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
            return (from u in securityContext.Users.Where(n => userLabels.Contains(n.UserName) && n.AuthenticationType.AuthenticationTypeName == userAuthenticationType) 
                join p in securityContext.UserProperties on u.UserId equals p.UserId select p).ToArray();
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given user through its roles
        /// </summary>
        /// <param name="user">the user for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given user</returns>
        public IEnumerable<Permission> GetPermissions(User user)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return (from p in (from r in securityContext.Users.First(n => n.UserName == user.UserName && n.AuthenticationType.AuthenticationTypeName == user.AuthenticationType).TenantUsers.SelectMany(n => n.Roles) select r.Role.RolePermissions).SelectMany(rp => rp) select new Permission
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
            return (from u in securityContext.Users.Where(n => userLabels.Contains(n.UserName) && n.AuthenticationType.AuthenticationTypeName == userAuthenticationType)
                    join tr in securityContext.TenantUsers on u.UserId equals tr.UserId
                    join ur in securityContext.UserRoles on tr.TenantUserId equals ur.TenantUserId
                    join r in securityContext.Roles on new {ur.RoleId, tr.TenantId} equals new { r.RoleId, r.TenantId }
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
            if (role is Models.Role dbRole)
            {
                return from p in dbRole.RolePermissions select p.Permission;
            }

            return (from p in securityContext.Roles.First(r => r.RoleName == role.RoleName).RolePermissions select new Permission
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

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, true, false);
            return (from d in (from t in securityContext.Users.Where(n => userLabels.Contains(n.UserName))
                    join u in securityContext.TenantUsers on t.UserId equals u.UserId
                    select u.Tenant).Distinct()
                orderby d.DisplayName
                select new ScopeInfo {ScopeDisplayName = d.DisplayName, ScopeName = d.TenantName}).ToArray();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}

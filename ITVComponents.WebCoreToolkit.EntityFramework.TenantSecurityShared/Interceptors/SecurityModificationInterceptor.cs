using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DIIntegration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Threading;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.EntityFrameworkCore;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DataAnnotation;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Interceptors
{
    public class SecurityModificationInterceptor<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole> : SaveChangesInterceptor
    where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>, new()
    where TTenantUser: TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TTenant : Tenant
    where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        private List<TRoleRole> roros = new List<TRoleRole>();
        private List<TRolePermission> ropes = new List<TRolePermission>();
        private List<Action<DbContext>> contextActions = new List<Action<DbContext>>();

        private readonly IServiceProvider services;

        public SecurityModificationInterceptor(IServiceProvider services)
        {
            this.services = services;
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            return AsyncHelpers.RunSync(async () => await SavedChangesAsync(eventData, result).ConfigureAwait(false));
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (eventData.Context != null)
            {
                var callSave = false;
                try
                {
                    BuildPendingChanges(contextActions);
                    if (contextActions.Any())
                    {
                        contextActions.ForEach(n => n(eventData.Context));
                    }

                }
                finally
                {
                    callSave = contextActions.Count != 0;
                    contextActions.Clear();
                }

                if (callSave)
                {
                    eventData.Context.SaveChanges();
                }
            }

            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            return AsyncHelpers.RunSync(async () => await SavingChangesAsync(eventData, result).ConfigureAwait(false));
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (eventData.Context != null)
            {
                var l = eventData.Context.ChangeTracker.Entries().ToList();
                //eventData.Context.ChangeTracker
                foreach (var entry in l.Where(n => n.Metadata.ClrType == typeof(TRoleRole) ||
                                                   n.Metadata.ClrType == typeof(TRolePermission)))
                {
                    bool ableToProcess = true;
                    if (entry.Entity is TRoleRole rohi && (rohi.PermittedRoleId != null && rohi.PermissiveRoleId != null || rohi.PermittedRole != null && rohi.PermissiveRole != null))
                    {
                        if (eventData.Context.IsCyclicRoleInheritance(rohi.PermittedRole?.RoleId??rohi.PermittedRoleId.Value, rohi.PermissiveRole?.RoleId??rohi.PermissiveRoleId.Value)
                            && entry.State == EntityState.Added)
                        {
                            throw new InvalidOperationException("Cyclic Role-Inheritance detected!");
                        }

                        TRole permitted = rohi.PermittedRole ??
                                          eventData.Context.Set<TRole>().First(n => n.RoleId == rohi.PermittedRoleId);
                        TRole permissive = rohi.PermissiveRole ??
                                           eventData.Context.Set<TRole>().First(n => n.RoleId == rohi.PermissiveRoleId);
                        ableToProcess = (permitted.Tenant?.TenantId ?? permitted.TenantId) ==
                                        (permissive.Tenant?.TenantId ?? permissive.TenantId);
                    }

                    if (ableToProcess)
                    {
                        if (entry.State == EntityState.Added)
                        {
                            if (entry.Entity is TRoleRole roro)
                            {
                                roros.Add(roro);
                            }
                            else if (entry.Entity is TRolePermission rope)
                            {
                                ropes.Add(rope);
                            }
                        }
                        else if (entry.State == EntityState.Deleted)
                        {
                            if (entry.Entity is TRoleRole roro)
                            {
                                LoadCascadeProxies(eventData.Context, roro, contextActions, true);
                            }
                            else if (entry.Entity is TRolePermission rope)
                            {
                                LoadCascadeProxies(eventData.Context, rope, contextActions, true);
                            }
                            else if (entry.Entity is TRole ro)
                            {
                                LoadCascadeProxies(eventData.Context, ro, contextActions, true);
                            }
                            else if (entry.Entity is TTenantUser us)
                            {
                                LoadCascadeProxies(eventData.Context, us, contextActions, true);
                            }
                        }
                    }
                    else
                    {
                        HandleCrossTenantReference(eventData, entry, contextActions);
                    }
                }
            }

            return await base.SavingChangesAsync(eventData, result);
        }

        protected virtual void BuildPendingChanges(List<Action<DbContext>> modifyActions)
        {
            ProcessRoleInheritanceChanges(modifyActions);
            ProcessPermissionInheritanceChanges(modifyActions);

        }

        protected virtual void HandleCrossTenantReference(DbContextEventData eventData, EntityEntry entry, List<Action<DbContext>> modifyActions)
        {
            LogEnvironment.LogEvent("Cross-tenant Permission inheritance is not covered by this interceptor",
                LogSeverity.Report);
        }

        protected virtual void LoadCascadeProxies(DbContext context, TRoleRole roro,
            List<Action<DbContext>> modifyActions, bool addToEntity)
        {
            var tmp = context.Set<TRolePermission>().Where(n => n.RoleRoleId == roro.RoleRoleId).ToList();
            if (addToEntity && roro.ResultingLinks is not List<TRolePermission>)
            {
                roro.ResultingLinks = tmp;
            }

            tmp.ForEach(n => LoadCascadeProxies(context, n, modifyActions, false));
            modifyActions.Add(db => db.Set<TRolePermission>().RemoveRange(tmp));
        }

        protected virtual void LoadCascadeProxies(DbContext context, TRolePermission rope,
            List<Action<DbContext>> modifyActions, bool addToEntity)
        {
            var tmp = context.Set<TRolePermission>().Where(n => n.OriginId == rope.RolePermissionId).ToList();
            if (addToEntity && rope.RoleInheritanceChildren is not List<TRolePermission>)
            {
                rope.RoleInheritanceChildren = tmp;
            }

            tmp.ForEach(n => LoadCascadeProxies(context, n, modifyActions, false));
            modifyActions.Add(db => db.Set<TRolePermission>().RemoveRange(tmp));
        }

        protected virtual void LoadCascadeProxies(DbContext context, TRole ro, List<Action<DbContext>> modifyActions,
            bool addToEntity)
        {
            var tmprr1 = context.Set<TRoleRole>().Where(n => n.PermissiveRoleId == ro.RoleId).ToList();
            if (addToEntity && ro.PermittedRoles is not List<TRolePermission>)
            {
                ro.PermittedRoles = tmprr1;
            }

            tmprr1.ForEach(n => LoadCascadeProxies(context, n, modifyActions, false));
            modifyActions.Add(db => db.Set<TRoleRole>().RemoveRange(tmprr1));

            var tmprp1 = context.Set<TRolePermission>().Where(n => n.RoleId == ro.RoleId).ToList();
            if (addToEntity && ro.RolePermissions is not List<TRolePermission>)
            {
                ro.RolePermissions = tmprp1;
            }

            tmprp1.ForEach(n => LoadCascadeProxies(context, n, modifyActions, false));
            modifyActions.Add(db => db.Set<TRolePermission>().RemoveRange(tmprp1));

            var tmprr2 = context.Set<TRoleRole>().Where(n => n.PermittedRoleId == ro.RoleId).ToList();
            if (addToEntity && ro.PermissiveRoles is not List<TRolePermission>)
            {
                ro.PermissiveRoles = tmprr2;
            }

            tmprr2.ForEach(n => LoadCascadeProxies(context, n, modifyActions, false));
            modifyActions.Add(db => db.Set<TRoleRole>().RemoveRange(tmprr2));

            var tmpur = context.Set<TUserRole>().Where(n => n.RoleId == ro.RoleId).ToList();
            if (addToEntity && ro.UserRoles is not List<TUserRole>)
            {
                ro.UserRoles = tmpur;
            }

            modifyActions.Add(db => db.Set<TUserRole>().RemoveRange(tmpur));
        }

        protected virtual void LoadCascadeProxies(DbContext context, TTenantUser tu,
            List<Action<DbContext>> modifyActions, bool addToEntity)
        {
            var tmp = context.Set<TUserRole>().Where(n => n.TenantUserId == tu.TenantUserId).ToList();
            if (addToEntity && tu.Roles is not List<TUserRole>)
            {
                tu.Roles = tmp;
            }

            modifyActions.Add(db => db.Set<TUserRole>().RemoveRange(tmp));
        }

        private void ProcessPermissionInheritanceChanges(List<Action<DbContext>> modifyActions)
        {
            if (ropes.Any())
            {
                modifyActions.Add(ctx =>
                {
                    try
                    {
                        var ids = ropes.Select(n => n.RolePermissionId).ToArray();
                        var tmpRope = ctx.Set<TRolePermission>().Include(n => n.Role)
                            .ThenInclude(r => r.PermittedRoles)
                            .ThenInclude(rp => rp.PermittedRole)
                            .Where(n => ids.Contains(n.RolePermissionId) && n.Role.PermittedRoles.Any())
                            .Select(n => new
                            {
                                Items = n.Role.PermittedRoles.Where(pr => pr.PermittedRole.TenantId == n.Role.TenantId)
                                    .Select(trp => new TRolePermission
                                    {
                                        TenantId = n.TenantId,
                                        OriginId = n.RolePermissionId,
                                        PermissionId = n.PermissionId,
                                        RoleId = trp.PermittedRole.RoleId,
                                        RoleRoleId = trp.RoleRoleId
                                    })
                            }).SelectMany(itm => itm.Items);
                        ctx.Set<TRolePermission>().AddRange(tmpRope);
                    }
                    finally
                    {
                        ropes.Clear();
                    }
                });
            }
        }

        private void ProcessRoleInheritanceChanges(List<Action<DbContext>> modifyActions)
        {
            if (roros.Any())
            {
                modifyActions.Add(ctx =>
                {
                    try
                    {
                        var ids = roros.Select(n => n.RoleRoleId).ToArray();
                        var tmpRoro = ctx.Set<TRoleRole>().Include(n => n.PermissiveRole)
                            .ThenInclude(r => r.RolePermissions)
                            .Include(n => n.PermittedRole)
                            .Where(n => ids.Contains(n.RoleRoleId))
                            .Select(n => new
                            {
                                Items = n.PermissiveRole.RolePermissions.Select(np => new TRolePermission
                                {
                                    TenantId = np.TenantId,
                                    OriginId = np.RolePermissionId,
                                    PermissionId = np.PermissionId,
                                    RoleId = n.PermittedRole.RoleId,
                                    RoleRoleId = n.RoleRoleId
                                })
                            }).SelectMany(itm => itm.Items);
                        ctx.Set<TRolePermission>().AddRange(tmpRoro);
                    }
                    finally
                    {
                        roros.Clear();
                    }
                });
            }
        }
    }
}
